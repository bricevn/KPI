// Issues tab — search + open/closed groups (open first) + expandable detail
// (labels, MR approvers, label-event timeline).
(function () {
  const { useState } = React;
  const A = window.APP;
  const LABEL = { uiux: 'Prod::UI/UX', dev: 'Prod::Code In Progress', review: 'Prod::Code review', qawait: 'Prod::QA Backlog', qa: 'Prod::QA InProgress', tofix: 'Prod::To Fix', po: 'Prod:: PO Validation' };
  const PHASE_OF = { 'Prod::UI/UX': 'uiux', 'Prod::Code In Progress': 'dev', 'Prod::Code review': 'review', 'Prod::QA Backlog': 'qawait', 'Prod::QA InProgress': 'qa', 'Prod::To Fix': 'tofix', 'Prod:: PO Validation': 'po' };

  function labelColor(l, d) {
    if (l.startsWith('Type::')) return window.typeColor(d.type);
    if (PHASE_OF[l]) return window.phaseColor(PHASE_OF[l]);
    return 'var(--ink-faint)';
  }

  function events(d) {
    const evs = [];
    Object.entries(d.seg).forEach(([k, segs]) => segs.forEach(([a, b, who]) => {
      evs.push({ day: a, action: 'add', label: LABEL[k] || k, phase: k, user: who || d.assignees[0] });
      evs.push({ day: b, action: 'remove', label: LABEL[k] || k, phase: k, user: who || d.assignees[0] });
    }));
    return evs.sort((x, y) => x.day - y.day || (x.action === 'add' ? -1 : 1));
  }

  function StatusBadge({ state }) {
    return <span className={'st-badge ' + (state === 'closed' ? 'st-closed' : 'st-open')}><span className="sd"></span>{state === 'closed' ? 'Fermée' : 'Ouverte'}</span>;
  }

  function Row({ d }) {
    const [open, setOpen] = useState(false);
    return (
      <div className="issue">
        <div className="issue-hd" onClick={() => setOpen((o) => !o)}>
          <span className={'issue-chev' + (open ? ' open' : '')}>{window.ICONS.chevron}</span>
          <window.IssueLink iid={d.iid} />
          <span className="issue-ttl">{d.title}</span>
          <span className="issue-meta">
            <span className="chip"><span className="dot" style={{ background: window.typeColor(d.type) }}></span>{A.typeByKey[d.type].short}</span>
            {d.mrCount > 0 && <span className="chip">{window.ICONS.git}{d.mrCount} MR</span>}
            <span className="wchip-l" title="Poids estimé (Fibonacci)"><span className="lk">Poids</span>{d.weight}</span>
            <span className="av-stack">{d.assignees.map((a) => <window.Avatar key={a} pid={a} />)}</span>
            <StatusBadge state={d.state} />
          </span>
        </div>
        {open &&
        <div className="issue-body">
            <div className="ib-grid">
              <div className="ib-row">
                <span className="ib-k">Labels</span>
                <span className="ib-v ib-chips">{d.labels.map((l) => <span key={l} className="lbl-chip"><span className="dot" style={{ background: labelColor(l, d) }}></span>{l}</span>)}</span>
              </div>
              <div className="ib-row">
                <span className="ib-k">Merge requests</span>
                <span className="ib-v">
                  {d.mrCount > 0 ?
                  <span className="mr-line">
                      <span className="chip">{window.ICONS.git}!{d.iid % 900 + 120} · {d.state === 'closed' ? 'mergée' : 'ouverte'}</span>
                      {d.approvers.length ?
                      <span className="mr-appr">approuvée par <span className="av-stack">{d.approvers.map((a) => <window.Avatar key={a} pid={a} size={20} />)}</span> {d.approvers.map((a) => A.peopleById[a].name).join(', ')}</span> :
                      <span className="muted">en attente d’approbation</span>}
                    </span> :
                  <span className="muted">Aucune merge request liée</span>}
                </span>
              </div>
            </div>
            <div className="ib-k" style={{ margin: '4px 0 6px' }}>Historique des labels</div>
            <table className="evt">
              <thead><tr><th>Date</th><th>Action</th><th>Label</th><th>Auteur</th></tr></thead>
              <tbody>
                {events(d).map((e, i) =>
                <tr key={i}>
                    <td className="muted">{A.cal.fmtDay(e.day)}</td>
                    <td className={e.action === 'add' ? 'act-add' : 'act-rm'}>{e.action === 'add' ? 'ajout' : 'retrait'}</td>
                    <td><span className="type"><span className="dot" style={{ background: window.phaseColor(e.phase) }}></span>{e.label}</span></td>
                    <td><span style={{ display: 'inline-flex', alignItems: 'center', gap: 7 }}><window.Avatar pid={e.user} size={20} />{A.peopleById[e.user] ? A.peopleById[e.user].name : '—'}</span></td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>}
      </div>);
  }

  function Group({ title, items, tone }) {
    if (!items.length) return null;
    return (
      <div className="issue-group">
        <div className="issue-group-hd"><span className="igh-pip" style={{ background: tone }}></span>{title}<span className="igh-n">{items.length}</span></div>
        {items.map((d) => <Row key={d.iid} d={d} />)}
      </div>);
  }

  window.TabIssues = function TabIssues() {
    const [q, setQ] = useState('');
    const list = A.detail.filter((d) => !q || ('' + d.iid).includes(q) || d.title.toLowerCase().includes(q.toLowerCase()));
    const opened = list.filter((d) => d.state === 'open');
    const closed = list.filter((d) => d.state === 'closed');
    return (
      <React.Fragment>
        <div className="search">
          {window.ICONS.search}
          <input placeholder="Rechercher par IID ou titre…" value={q} onChange={(e) => setQ(e.target.value)} />
        </div>
        <div style={{ marginTop: 16 }}>
          {list.length ?
          <React.Fragment>
              <Group title="Ouvertes" items={opened} tone="var(--c-warn)" />
              <Group title="Fermées" items={closed} tone="var(--c-done)" />
            </React.Fragment> :
          <div className="empty">Aucune issue ne correspond.</div>}
        </div>
      </React.Fragment>);

  };
})();
