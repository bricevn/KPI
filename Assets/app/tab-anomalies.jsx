// Anomalies tab — summary cards + detailed expandable lists.
(function () {
  const { useState } = React;
  const A = window.APP;
  const CATS = [
  { key: 'noAssignee', label: 'Sans assignation', tone: 'warn', desc: 'Aucun responsable assigné' },
  { key: 'noMilestone', label: 'Sans milestone', tone: 'warn', desc: 'Non rattachée à une milestone' },
  { key: 'noWeight', label: 'Sans poids', tone: 'warn', desc: 'Pas d’estimation de poids' },
  { key: 'noType', label: 'Sans type', tone: 'warn', desc: 'Aucun label Type::*' },
  { key: 'noPrio', label: 'Sans prio', tone: 'warn', desc: 'Aucune priorité définie' },
  { key: 'noApproval', label: 'Sans approval', tone: 'bad', desc: 'Aucune approbation de MR' },
  { key: 'stale', label: 'Stagnantes (≥ 30 j)', tone: 'bad', desc: 'Ouvertes depuis 30 jours ou plus' },
  { key: 'multiType', label: 'Plusieurs Type', tone: 'warn', desc: 'Plus d’un label Type::*' },
  { key: 'closedNoMR', label: 'Fermées sans MR liées', tone: 'bad', desc: 'Closes sans merge request' },
  { key: 'closedOpenMR', label: 'Fermées avec MR ouvertes', tone: 'bad', desc: 'Closes alors qu’une MR reste ouverte' }];

  const toneVar = (t) => t === 'bad' ? 'var(--c-bad)' : 'var(--c-warn)';
  const catColor = (c, n) => n ? toneVar(c.tone) : 'var(--c-good)';

  function IssueLine({ d }) {
    return (
      <div className="arow">
        <window.IssueLink iid={d.iid} />
        <span className="arow-ttl">{d.title}</span>
        <span className="chip"><span className="dot" style={{ background: window.typeColor(d.type) }}></span>{A.typeByKey[d.type].short}</span>
        <span className="wchip-l" title="Poids estimé (Fibonacci)"><span className="lk">Poids</span>{d.weight}</span>
        <span className="av-stack">{d.assignees.map((a) => <window.Avatar key={a} pid={a} size={22} />)}</span>
        <span className={'st-badge ' + (d.state === 'closed' ? 'st-closed' : 'st-open')}>{d.state === 'closed' ? 'Fermée' : 'Ouverte'}</span>
      </div>);
  }

  window.TabAnomalies = function TabAnomalies() {
    const [open, setOpen] = useState('noAssignee');
    const totalAnom = CATS.reduce((s, c) => s + A.anomalies[c.key].length, 0);
    return (
      <React.Fragment>
        <div className="anom-short">
          {CATS.map((c) => {
            const n = A.anomalies[c.key].length;
            return (
              <button key={c.key} className={'acard' + (open === c.key ? ' on' : '')} onClick={() => setOpen(c.key)}>
                <span className="acard-n" style={{ color: catColor(c, n) }}>{n}</span>
                <span className="acard-t">{c.label}</span>
              </button>);
          })}
        </div>

        <div className="col" style={{ marginTop: 16 }}>
          {CATS.map((c) => {
            const list = A.anomalies[c.key];
            const isOpen = open === c.key;
            return (
              <div key={c.key} className="pnl alist">
                <button className="ahd" onClick={() => setOpen(isOpen ? '' : c.key)}>
                  <span className="ahd-pip" style={{ background: catColor(c, list.length) }}></span>
                  <span className="ahd-title">{c.label}</span>
                  <span className="ahd-desc">{c.desc}</span>
                  <span className="ahd-count">{list.length}</span>
                  <span className={'ahd-chev' + (isOpen ? ' open' : '')}>{window.ICONS.chevron}</span>
                </button>
                {isOpen && (list.length ? <div className="alist-body">{list.map((d) => <IssueLine key={d.iid} d={d} />)}</div> : <div className="empty">Aucune issue concernée.</div>)}
              </div>);
          })}
        </div>
      </React.Fragment>);

  };
})();