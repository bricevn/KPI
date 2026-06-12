// Calendrier tab — horizontal Gantt of phases per issue.
(function () {
  const { useState } = React;
  const A = window.APP,DAYS = A.cal.DAYS,TODAY = A.cal.TODAY;
  // TOUTES les phases (segments Gantt) — y compris non chronométrées (uiux) — depuis la config ; repli standard.
  const PHASES = (A.periods && A.periods.length) ? A.periods.map((p) => p.key) : ['uiux', 'dev', 'review', 'qawait', 'qa', 'tofix', 'po'];
  const pos = (d) => d / DAYS * 100;

  window.TabCalendar = function TabCalendar() {
    const [hidden, setHidden] = useState(() => new Set());
    const { s, onSort, arrow } = window.useSort('', 'desc');
    const { scrollRef, dragging, Nav, gridStyle } = window.useGanttNav(900);
    const toggle = (k) => setHidden((s) => {const n = new Set(s);n.has(k) ? n.delete(k) : n.add(k);return n;});
    const phaseDate = (d, k) => {const segs = d.seg[k];return segs && segs.length ? segs[0][0] : 1e9;};
    let rows = [...A.detail];
    if (s.key) {rows.sort((a, b) => {const r = phaseDate(a, s.key) - phaseDate(b, s.key);return s.dir === 'desc' ? -r : r;});} else
    {rows.sort((a, b) => a.start - b.start);}
    const Eye = ({ off }) => <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">{off ? <g><path d="M2 2l20 20" /><path d="M6.7 6.7A10 10 0 001 12s4 7 11 7a10 10 0 005.3-1.5M9.9 4.2A10 10 0 0112 4c7 0 11 7 11 7a18 18 0 01-2.2 3" /></g> : <g><path d="M1 12s4-7 11-7 11 7 11 7-4 7-11 7-11-7-11-7z" /><circle cx="12" cy="12" r="3" /></g>}</svg>;

    return (
      <React.Fragment>
        <div className="cal-toolbar">
          <span className="muted" style={{ fontSize: 12, fontWeight: 600 }}>{window.t('cal.phases')}</span>
          <window.InfoTip text={window.t('cal.tip')} />
          <div className="cal-legend" style={{ marginLeft: 6 }}>
            {PHASES.map((k) =>
            <span key={k} className={'cal-lg' + (hidden.has(k) ? ' off' : '') + (s.key === k ? ' sort' : '')}>
                <span className="sw" style={{ background: window.phaseColor(k) }}></span>
                <span className="cal-lg-lbl" onClick={() => onSort(k)}>{window.PHASE_NAME[k]} <span className="ar">{arrow(k)}</span></span>
                <button className="cal-eye" title={window.t('cal.hideShow')} onClick={() => toggle(k)}><Eye off={hidden.has(k)} /></button>
              </span>
            )}
          </div>
          <Nav />
        </div>

        <div className="gantt">
          <div className={'gantt-scroll gantt-drag' + (dragging ? ' grabbing' : '')} ref={scrollRef}>
            <div className="gantt-grid" style={gridStyle}>
              <div className="gantt-axis">
                <div className="gantt-axis-corner">{window.t('cal.issue')}</div>
                {Array.from({ length: A.cal.WEEKS }, (_, i) => <span key={i} className="wk">{window.t('common.weekShort')}{i + 1}</span>)}
              </div>
              {rows.map((d) =>
              <div key={d.iid} className="grow">
                  <div className="glabel">
                    <window.IssueLink iid={d.iid} />
                    <span className="nm">{d.title}</span>
                    <span className="av-stack">{d.assignees.map((a) => <window.Avatar key={a} pid={a} size={20} />)}</span>
                  </div>
                  <div className="gtrack">
                    {/* milestone + today markers */}
                    <span className="gmark ms" style={{ left: '0%' }}></span>
                    <span className="gmark today" style={{ left: pos(TODAY) + '%' }}></span>
                    {PHASES.filter((k) => !hidden.has(k)).flatMap((k) => (d.seg[k] || []).map(([a, b, who], idx) =>
                  <span key={k + idx} className="gseg" title={`${window.PHASE_NAME[k]} · ${A.cal.fmtDay(a)}→${A.cal.fmtDay(b)}${who ? ' · ' + (A.peopleById[who] ? A.peopleById[who].name : '') : ''}`}
                  style={{ left: pos(a) + '%', width: Math.max(0.8, pos(b - a)) + '%', background: window.phaseColor(k), opacity: k === 'dev' && who ? 0.92 : 1 }}></span>
                  ))}
                  </div>
                </div>
              )}
            </div>
          </div>
        </div>
        <div className="muted" style={{ fontSize: 11.5, marginTop: 8, display: 'flex', gap: 18 }}>
          <span><span style={{ display: 'inline-block', width: 14, height: 2, background: 'var(--accent)', verticalAlign: 'middle', marginRight: 5 }}></span>{window.t('cal.today')} ({A.cal.fmtDay(TODAY)})</span>
        </div>
      </React.Fragment>);

  };
})();