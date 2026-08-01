using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Kpi.Config;

namespace Kpi.Canny;

/// <summary>Sortie de la consolidation : le dataset analytique + les commentaires détaillés (JSON), et les comptes.</summary>
public sealed record CannyBuildOutput(string DatasetJson, string CommentsJson, CannyExtractResult Result);

/// <summary>
/// Consolidation du dataset Canny — PORT FIDÈLE de <c>Desktop/Canny/scripts/build-dataset.js</c>.
/// Produit la même forme analytique (posts enrichis avec SLA heures ouvrées, roadmaps, timeInStatus,
/// agrégats) que le pipeline Node, PLUS l'extraction des numéros d'issue/epic GitLab depuis les
/// <c>details</c> des posts et le texte des commentaires (rapprochement Canny↔GitLab, absent du Node).
/// Le SLA (seuil, heures ouvrées, fuseau) est piloté par <see cref="CannyConfig"/>.
/// </summary>
public static class CannyDatasetBuilder
{
    private const long Hour = 3_600_000L;
    private const long Day = 24 * Hour;

    private static readonly JsonSerializerOptions OutJson = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly Regex IssueRx = new(@"/-/issues/(\d+)", RegexOptions.Compiled);
    private static readonly Regex EpicRx = new(@"/-/epics/(\d+)", RegexOptions.Compiled);
    private static readonly Regex NonRoadmapKey = new("[^0-9r]", RegexOptions.Compiled);
    private static readonly Regex PlannedFieldRx = new(@"planned\s*release", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static CannyBuildOutput Build(
        CannyConfig cfg,
        List<CannyBoardRaw> boards, List<CannyCategoryRaw> categories, List<CannyTagRaw> tags,
        List<CannyPostRaw> posts, List<CannyCommentRaw> comments, int votesCount,
        List<CannyUserRaw> users, List<CannyStatusChangeRaw> statusChanges)
    {
        var tz = ResolveTz(cfg.TimeZone);
        var slaMs = (long)Math.Max(0, cfg.SlaHours) * Hour;
        var bizDay = (long)Math.Max(0, cfg.WorkEndHour - cfg.WorkStartHour) * Hour;

        // NOW = date la plus récente observée (comme le Node) — sert de borne pour les posts sans réponse.
        long now = 0;
        foreach (var iso in posts.Select(p => p.Created).Concat(comments.Select(c => c.Created)).Concat(statusChanges.Select(s => s.Created)))
        {
            var t = ParseMs(iso);
            if (t.HasValue && t.Value > now) now = t.Value;
        }

        long BusinessMs(long aMs, long bMs)
        {
            var s = TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeMilliseconds(aMs), tz).DateTime;
            var e = TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeMilliseconds(bMs), tz).DateTime;
            if (e <= s) return 0;
            double tot = 0;
            var cur = s;
            while (cur < e)
            {
                var dayStart = cur.Date;
                var w = (int)cur.DayOfWeek; // Dimanche=0 … Samedi=6
                if (w >= 1 && w <= 5)
                {
                    var os = cur > dayStart.AddHours(cfg.WorkStartHour) ? cur : dayStart.AddHours(cfg.WorkStartHour);
                    var oe = e < dayStart.AddHours(cfg.WorkEndHour) ? e : dayStart.AddHours(cfg.WorkEndHour);
                    if (oe > os) tot += (oe - os).TotalMilliseconds;
                }
                cur = dayStart.AddDays(1);
            }
            return (long)tot;
        }

        // ---- indexation des commentaires ----
        static bool IsRealText(CannyCommentRaw c) => !string.IsNullOrWhiteSpace(c.Value);
        var firstAdminText = new Dictionary<string, long>();
        var realCountByPost = new Dictionary<string, int>();
        var firstStatus = new Dictionary<string, long>();
        var commentTextByPost = new Dictionary<string, List<string>>();
        foreach (var c in comments)
        {
            var pid = c.Post?.Id;
            if (string.IsNullOrEmpty(pid)) continue;
            if (!string.IsNullOrEmpty(c.Value))
            {
                if (!commentTextByPost.TryGetValue(pid, out var texts)) { texts = new(); commentTextByPost[pid] = texts; }
                texts.Add(c.Value!);
            }
            if (IsRealText(c))
            {
                realCountByPost[pid] = realCountByPost.GetValueOrDefault(pid) + 1;
                if (c.Author?.IsAdmin == true)
                {
                    var t = ParseMs(c.Created);
                    if (t.HasValue && (!firstAdminText.TryGetValue(pid, out var cur) || t.Value < cur)) firstAdminText[pid] = t.Value;
                }
            }
        }
        foreach (var sc in statusChanges)
        {
            var pid = sc.Post?.Id;
            if (string.IsNullOrEmpty(pid)) continue;
            var t = ParseMs(sc.Created);
            if (t.HasValue && (!firstStatus.TryGetValue(pid, out var cur) || t.Value < cur)) firstStatus[pid] = t.Value;
        }

        // ---- roadmaps (normalisation + tables de référence) ----
        var roadmapByKey = new Dictionary<string, string>();
        var roadmapMap = new Dictionary<string, CannyRoadmapRaw>();
        foreach (var p in posts)
            foreach (var r in p.Roadmaps ?? new())
            {
                if (!string.IsNullOrEmpty(r.Name)) roadmapByKey[Norm(r.Name)] = r.Name;
                roadmapMap[r.Id] = r;
            }
        var refRoadmaps = roadmapMap.Values.OrderBy(r => r.Name, StringComparer.Ordinal).ToList();

        string? Current(CannyPostRaw p)
        {
            var rms = p.Roadmaps ?? new();
            if (rms.Count == 0) return null;
            var act = rms.Where(r => !r.Archived).ToList();
            var pool = act.Count > 0 ? act : rms;
            return pool.Select(r => r.Name).OrderBy(n => n, StringComparer.Ordinal).Last();
        }
        string? PlannedRaw(CannyPostRaw p)
        {
            foreach (var cf in p.CustomFields ?? new())
            {
                if (cf.Name == null || !PlannedFieldRx.IsMatch(cf.Name)) continue;
                string v = cf.Value.ValueKind switch
                {
                    JsonValueKind.Array => string.Join(", ", cf.Value.EnumerateArray().Select(x => x.ToString())),
                    JsonValueKind.String => cf.Value.GetString() ?? "",
                    JsonValueKind.Null or JsonValueKind.Undefined => "",
                    _ => cf.Value.ToString(),
                };
                v = v.Trim();
                if (v.Length > 0 && v != "none" && v != "-") return v;
            }
            return null;
        }

        // ---- posts enrichis ----
        var enriched = posts.Select(p =>
        {
            var created = ParseMs(p.Created) ?? 0;
            long? ft = firstAdminText.TryGetValue(p.Id, out var f) ? f : null;
            long? fss = firstStatus.TryGetValue(p.Id, out var g) ? g : null;
            long? respTs = (ft == null && fss == null) ? null
                : (ft == null) ? fss
                : (fss == null) ? ft
                : Math.Min(ft.Value, fss.Value);
            string? via = respTs == null ? null : (ft != null && (fss == null || ft.Value <= fss.Value) ? "comment" : "status");
            var bizToResp = respTs != null ? BusinessMs(created, respTs.Value) : BusinessMs(created, now);
            var breach = bizToResp > slaMs;
            var raw = PlannedRaw(p);
            string? planned = raw != null && roadmapByKey.TryGetValue(Norm(raw), out var pn) ? pn : null;
            var cur = Current(p);
            commentTextByPost.TryGetValue(p.Id, out var ctext);
            return new
            {
                id = p.Id,
                title = p.Title,
                url = p.Url,
                boardId = p.Board?.Id,
                board = p.Board?.Name,
                status = p.Status,
                valide = p.Status == "complete",
                score = p.Score,
                commentCount = p.CommentCount,
                realCommentCount = realCountByPost.GetValueOrDefault(p.Id),
                created = p.Created,
                authorId = p.Author?.Id,
                categoryId = p.Category?.Id,
                tagIds = (p.Tags ?? new()).Select(t => t.Id).ToList(),
                roadmapIds = (p.Roadmaps ?? new()).Select(r => r.Id).ToList(),
                currentRelease = cur,
                plannedRelease = planned,
                plannedReleaseRaw = raw,
                plannedMatched = raw != null ? (bool?)roadmapByKey.ContainsKey(Norm(raw)) : null,
                onTrack = planned != null && cur != null ? (bool?)(Norm(planned) == Norm(cur)) : null,
                firstResponseAt = respTs != null ? IsoUtc(respTs.Value) : null,
                firstResponseVia = via,
                responseBusinessHours = Math.Round(bizToResp / (double)Hour, 2, MidpointRounding.AwayFromZero),
                responseWithin4h = respTs != null && bizToResp <= slaMs,
                responseWithinDay = respTs != null && bizToResp <= bizDay,
                answered = respTs != null,
                slaBreach = breach,
                slaState = respTs == null ? (breach ? "no_response_breached" : "pending_in_window") : (breach ? "breached" : "compliant"),
                // Rapprochement GitLab (nouveau) : numéros d'issue/epic trouvés dans details + commentaires.
                gitlabIssues = ExtractIds(IssueRx, p.Details, ctext),
                gitlabEpics = ExtractIds(EpicRx, p.Details, ctext),
            };
        }).ToList();

        // ---- commentaires détaillés (couche détail) ----
        var detailComments = comments.Select(c => new
        {
            id = c.Id,
            postId = c.Post?.Id,
            authorId = c.Author?.Id,
            authorName = c.Author?.Name,
            isAdmin = c.Author?.IsAdmin ?? false,
            @internal = c.Internal,
            isStatusMarker = !IsRealText(c) && !string.IsNullOrEmpty(c.Status),
            hasText = IsRealText(c),
            status = c.Status,
            created = c.Created,
            text = c.Value ?? "",
        }).ToList();

        // ---- agrégats ----
        var roadmapValidation = refRoadmaps.Select(r =>
        {
            var inRm = enriched.Where(e => e.roadmapIds.Contains(r.Id)).ToList();
            return new { id = r.Id, name = r.Name, archived = r.Archived, total = inRm.Count, valide = inRm.Count(e => e.valide), closed = inRm.Count(e => e.status == "closed") };
        }).ToList();

        var plannedDict = new Dictionary<string, PlannedAgg>();
        foreach (var e in enriched)
        {
            if (string.IsNullOrEmpty(e.plannedRelease)) continue;
            if (!plannedDict.TryGetValue(e.plannedRelease!, out var a)) { a = new PlannedAgg { release = e.plannedRelease! }; plannedDict[e.plannedRelease!] = a; }
            a.planifie++;
            if (e.valide) a.valide++; else a.nonValide++;
            if (e.onTrack == false) a.glisse++;
        }
        var plannedByRoadmap = plannedDict.Values.OrderBy(a => a.release, StringComparer.Ordinal).ToList();

        var timeInStatus = ComputeTimeInStatus(posts, statusChanges);

        var dataset = new
        {
            meta = new
            {
                source = string.IsNullOrWhiteSpace(cfg.Subdomain) ? "canny" : cfg.Subdomain,
                extractedAt = now > 0 ? IsoUtc(now) : null,
                generatedFrom = "Canny API v1 (KPI)",
                slaConfig = new { hours = cfg.SlaHours, tz = cfg.TimeZone, workStart = cfg.WorkStartHour, workEnd = cfg.WorkEndHour, days = "Mon-Fri" },
                definitions = new { valide = "status === 'complete'", response = "earliest of first admin text comment OR first status change", glisse = "currentRelease !== plannedRelease" },
                counts = new { posts = enriched.Count, comments = detailComments.Count, votes = votesCount, users = users.Count, statusChanges = statusChanges.Count, boards = boards.Count, categories = categories.Count, tags = tags.Count, roadmaps = refRoadmaps.Count },
            },
            boards = boards.Select(b => new { id = b.Id, name = b.Name, isPrivate = b.IsPrivate, postCount = b.PostCount, created = b.Created }).ToList(),
            categories = categories.Select(c => new { id = c.Id, name = c.Name, boardId = c.Board?.Id, parentID = c.ParentID, postCount = c.PostCount }).ToList(),
            tags = tags.Select(t => new { id = t.Id, name = t.Name, boardId = t.Board?.Id, postCount = t.PostCount }).ToList(),
            roadmaps = refRoadmaps.Select(r => new { id = r.Id, name = r.Name, url = r.Url, archived = r.Archived, created = r.Created, postCount = r.PostCount }).ToList(),
            users = users.Select(u => new { id = u.Id, name = u.Name, email = u.Email, isAdmin = u.IsAdmin }).ToList(),
            posts = enriched,
            aggregates = new
            {
                postsByStatus = Tally(enriched, e => e.status),
                postsByBoard = Tally(enriched, e => e.board),
                roadmapValidation,
                plannedVsDoneByRoadmap = plannedByRoadmap,
                sla = new
                {
                    compliant = enriched.Count(e => e.slaState == "compliant"),
                    breached = enriched.Count(e => e.slaState == "breached"),
                    noResponseBreached = enriched.Count(e => e.slaState == "no_response_breached"),
                    pending = enriched.Count(e => e.slaState == "pending_in_window"),
                    within4h = enriched.Count(e => e.responseWithin4h),
                    withinDay = enriched.Count(e => e.responseWithinDay),
                },
                timeInStatus,
            },
        };

        var result = new CannyExtractResult
        {
            Posts = enriched.Count,
            Comments = detailComments.Count,
            Votes = votesCount,
            Users = users.Count,
            StatusChanges = statusChanges.Count,
            Boards = boards.Count,
            Categories = categories.Count,
            Tags = tags.Count,
            Roadmaps = refRoadmaps.Count,
            ExtractedAt = now > 0 ? IsoUtc(now) : "",
        };

        return new CannyBuildOutput(
            JsonSerializer.Serialize(dataset, OutJson),
            JsonSerializer.Serialize(detailComments, OutJson),
            result);
    }

    // ---- temps passé dans chaque statut (port de l'IIFE timeInStatus) ----
    private static Dictionary<string, TisEntry> ComputeTimeInStatus(List<CannyPostRaw> posts, List<CannyStatusChangeRaw> statusChanges)
    {
        var chByPost = new Dictionary<string, List<(string status, long t)>>();
        foreach (var c in statusChanges)
        {
            var pid = c.Post?.Id;
            if (string.IsNullOrEmpty(pid)) continue;
            var t = ParseMs(c.Created);
            if (!t.HasValue) continue;
            if (!chByPost.TryGetValue(pid, out var l)) { l = new(); chByPost[pid] = l; }
            l.Add((c.Status ?? "", t.Value));
        }

        var durs = new Dictionary<string, List<long>>();
        var visits = new Dictionary<string, int>();
        var postSets = new Dictionary<string, HashSet<string>>();
        void Visit(string pid, string status, long start, long? end)
        {
            visits[status] = visits.GetValueOrDefault(status) + 1;
            if (!postSets.TryGetValue(status, out var hs)) { hs = new(); postSets[status] = hs; }
            hs.Add(pid);
            if (end.HasValue) { var d = end.Value - start; if (d > 0) { if (!durs.TryGetValue(status, out var dl)) { dl = new(); durs[status] = dl; } dl.Add(d); } }
        }
        foreach (var p in posts)
        {
            var chs = chByPost.TryGetValue(p.Id, out var cl) ? cl.OrderBy(x => x.t).ToList() : new List<(string status, long t)>();
            long segStart = ParseMs(p.Created) ?? 0;
            var segStatus = "open"; // statut initial
            foreach (var ch in chs) { Visit(p.Id, segStatus, segStart, ch.t); segStart = ch.t; segStatus = ch.status; }
            Visit(p.Id, segStatus, segStart, null); // séjour EN COURS (compté, sans durée)
        }

        var res = new Dictionary<string, TisEntry>();
        foreach (var s in visits.Keys)
        {
            var a = durs.TryGetValue(s, out var dl) ? dl : new List<long>();
            res[s] = new TisEntry
            {
                posts = postSets[s].Count,
                visits = visits[s],
                staysPerPost = Math.Round(visits[s] / (double)postSets[s].Count, 2, MidpointRounding.AwayFromZero),
                completedStays = a.Count,
                // MidpointRounding.AwayFromZero pour coller à Math.round() de JS (demi-sup ; valeurs positives).
                avgMs = a.Count > 0 ? (long)Math.Round(a.Average(), MidpointRounding.AwayFromZero) : null,
                medianMs = a.Count > 0 ? (long)Math.Round(Median(a), MidpointRounding.AwayFromZero) : null,
            };
        }
        return res;
    }

    // ---- helpers ----
    private static Dictionary<string, int> Tally<T>(IEnumerable<T> arr, Func<T, string?> key)
    {
        var m = new Dictionary<string, int>();
        foreach (var x in arr) { var k = key(x); if (k != null) m[k] = m.GetValueOrDefault(k) + 1; }
        return m;
    }

    private static List<int> ExtractIds(Regex rx, string? details, List<string>? commentTexts)
    {
        var set = new SortedSet<int>();
        void Scan(string? txt) { if (string.IsNullOrEmpty(txt)) return; foreach (Match m in rx.Matches(txt)) if (int.TryParse(m.Groups[1].Value, out var n)) set.Add(n); }
        Scan(details);
        if (commentTexts != null) foreach (var t in commentTexts) Scan(t);
        return set.ToList();
    }

    private static string Norm(string? s)
    {
        s = (s ?? "").ToLowerInvariant().Replace("roadmap", "");
        return NonRoadmapKey.Replace(s, "");
    }

    private static double Median(List<long> a)
    {
        if (a.Count == 0) return 0;
        var s = a.OrderBy(x => x).ToList();
        var m = s.Count / 2;
        return s.Count % 2 == 1 ? s[m] : (s[m - 1] + s[m]) / 2.0;
    }

    private static long? ParseMs(string? iso)
    {
        if (string.IsNullOrWhiteSpace(iso)) return null;
        return DateTimeOffset.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces, out var dto)
            ? dto.ToUnixTimeMilliseconds() : null;
    }

    private static string IsoUtc(long ms) =>
        DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

    private static TimeZoneInfo ResolveTz(string? id)
    {
        foreach (var candidate in new[] { id, "Europe/Paris", "Romance Standard Time" })
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            try { return TimeZoneInfo.FindSystemTimeZoneById(candidate); } catch { /* essai suivant */ }
        }
        return TimeZoneInfo.Utc;
    }

    private sealed class PlannedAgg
    {
        public string release { get; set; } = "";
        public int planifie { get; set; }
        public int valide { get; set; }
        public int nonValide { get; set; }
        public int glisse { get; set; }
    }

    private sealed class TisEntry
    {
        public int posts { get; set; }
        public int visits { get; set; }
        public double staysPerPost { get; set; }
        public int completedStays { get; set; }
        public long? avgMs { get; set; }
        public long? medianMs { get; set; }
    }
}
