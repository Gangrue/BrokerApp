import { useEffect, useMemo, useState } from 'react'
import './App.css'

type DashboardAction = {
  id: string
  borrowerName: string
  loanNumber: string
  title: string
  section: string
  bucket: string
  priority: string
  dueDate: string
}

type DashboardSummary = {
  overdueCount: number
  dueTodayCount: number
  upcomingCount: number
  openActions: DashboardAction[]
}

type ActionStatus = 'open' | 'completed' | 'rescheduled'
type WorkspaceView = 'dashboard' | 'loans'

const emptyDashboard: DashboardSummary = {
  overdueCount: 0,
  dueTodayCount: 0,
  upcomingCount: 0,
  openActions: [],
}

const sectionCopy: Record<string, string> = {
  Borrower: 'Borrower conditions',
  Title: 'Title conditions',
  Realtor: 'Realtor follow-up',
}

const pipelineStages = ['New file', 'Processing', 'Condition review', 'Clear to close']

function formatDueDate(value: string) {
  const date = new Date(`${value}T00:00:00`)

  return new Intl.DateTimeFormat(undefined, {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  }).format(date)
}

function normalizeBucket(bucket: string) {
  return bucket.toLowerCase().replace(' ', '-')
}

function App() {
  const [dashboard, setDashboard] = useState<DashboardSummary>(emptyDashboard)
  const [searchTerm, setSearchTerm] = useState('')
  const [selectedActionId, setSelectedActionId] = useState<string | null>(null)
  const [view, setView] = useState<WorkspaceView>('dashboard')
  const [actionStatus, setActionStatus] = useState<Record<string, ActionStatus>>({})
  const [notes, setNotes] = useState<Record<string, string[]>>({})
  const [noteDraft, setNoteDraft] = useState('')
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()

    async function loadDashboard() {
      try {
        const response = await fetch('/api/v1/dashboard', {
          signal: controller.signal,
        })

        if (!response.ok) {
          throw new Error(`Dashboard request failed with ${response.status}`)
        }

        const summary = await response.json() as DashboardSummary
        setDashboard(summary)
        setSelectedActionId(summary.openActions[0]?.id ?? null)
        setError(null)
      } catch (caughtError) {
        if (caughtError instanceof DOMException && caughtError.name === 'AbortError') {
          return
        }

        setError(caughtError instanceof Error ? caughtError.message : 'Dashboard request failed')
      } finally {
        setIsLoading(false)
      }
    }

    void loadDashboard()

    return () => controller.abort()
  }, [])

  const activeActions = useMemo(() => (
    dashboard.openActions.filter((action) => actionStatus[action.id] !== 'completed')
  ), [dashboard.openActions, actionStatus])

  const filteredActions = useMemo(() => {
    const query = searchTerm.trim().toLowerCase()

    if (!query) {
      return activeActions
    }

    return activeActions.filter((action) =>
      [
        action.borrowerName,
        action.loanNumber,
        action.title,
        action.section,
        action.bucket,
        action.priority,
      ].some((value) => value.toLowerCase().includes(query)),
    )
  }, [activeActions, searchTerm])

  const selectedAction = useMemo(() => (
    activeActions.find((action) => action.id === selectedActionId)
    ?? filteredActions[0]
    ?? activeActions[0]
    ?? null
  ), [activeActions, filteredActions, selectedActionId])

  const visibleCounts = useMemo(() => ({
    overdue: activeActions.filter((action) => action.bucket === 'Overdue').length,
    dueToday: activeActions.filter((action) => action.bucket === 'Due Today').length,
    upcoming: activeActions.filter((action) => action.bucket === 'Upcoming').length,
  }), [activeActions])

  const pipelineLoans = useMemo(() => (
    activeActions.map((action, index) => ({
      loanNumber: action.loanNumber,
      borrowerName: action.borrowerName,
      stage: pipelineStages[Math.min(index + 1, pipelineStages.length - 1)],
      priority: action.priority,
      nextAction: action.title,
      dueDate: action.dueDate,
    }))
  ), [activeActions])

  const selectedNotes = selectedAction ? notes[selectedAction.id] ?? [] : []

  function completeSelectedAction() {
    if (!selectedAction) {
      return
    }

    setActionStatus((current) => ({
      ...current,
      [selectedAction.id]: 'completed',
    }))
    setNotes((current) => ({
      ...current,
      [selectedAction.id]: [
        `Completed from dashboard on ${new Date().toLocaleDateString()}.`,
        ...(current[selectedAction.id] ?? []),
      ],
    }))
  }

  function rescheduleSelectedAction() {
    if (!selectedAction) {
      return
    }

    setActionStatus((current) => ({
      ...current,
      [selectedAction.id]: 'rescheduled',
    }))
    setNotes((current) => ({
      ...current,
      [selectedAction.id]: [
        'Follow-up rescheduled for review.',
        ...(current[selectedAction.id] ?? []),
      ],
    }))
  }

  function addNote() {
    if (!selectedAction || !noteDraft.trim()) {
      return
    }

    setNotes((current) => ({
      ...current,
      [selectedAction.id]: [
        noteDraft.trim(),
        ...(current[selectedAction.id] ?? []),
      ],
    }))
    setNoteDraft('')
  }

  return (
    <main className="app-shell">
      <aside className="sidebar" aria-label="Primary">
        <div className="brand">
          <span className="brand-mark">BA</span>
          <span>
            <strong>Broker App</strong>
            <small>Loan workflow</small>
          </span>
        </div>
        <nav>
          <button className={view === 'dashboard' ? 'active' : ''} type="button" onClick={() => setView('dashboard')}>
            Dashboard
          </button>
          <button className={view === 'loans' ? 'active' : ''} type="button" onClick={() => setView('loans')}>
            Loans
          </button>
          <button type="button">Customers</button>
          <button type="button">Reports</button>
          <button type="button">Admin</button>
        </nav>
        <div className="sidebar-summary">
          <span>Queue health</span>
          <strong>{visibleCounts.overdue === 0 ? 'On track' : `${visibleCounts.overdue} overdue`}</strong>
        </div>
      </aside>

      <section className="workspace" id="dashboard">
        <header className="topbar">
          <div>
            <p className="eyebrow">Daily workflow</p>
            <h1>{view === 'dashboard' ? 'Processing dashboard' : 'Loan pipeline'}</h1>
          </div>
          <div className="topbar-actions">
            <input
              aria-label="Search loans and actions"
              onChange={(event) => setSearchTerm(event.target.value)}
              placeholder="Search loans or borrowers"
              value={searchTerm}
            />
            <button type="button">New action</button>
          </div>
        </header>

        <section className="metrics" aria-label="Action summary">
          <article className="metric-overdue">
            <span>Overdue</span>
            <strong>{visibleCounts.overdue}</strong>
            <small>Past business day</small>
          </article>
          <article className="metric-today">
            <span>Due today</span>
            <strong>{visibleCounts.dueToday}</strong>
            <small>Needs officer touch</small>
          </article>
          <article className="metric-upcoming">
            <span>Upcoming</span>
            <strong>{visibleCounts.upcoming}</strong>
            <small>Within active queue</small>
          </article>
          <article>
            <span>Open queue</span>
            <strong>{activeActions.length}</strong>
            <small>Visible work items</small>
          </article>
        </section>

        {view === 'dashboard' ? (
          <section className="content-grid">
            <div className="panel action-panel">
              <div className="panel-header">
                <div>
                  <h2>Open actions</h2>
                  <p>{filteredActions.length} visible</p>
                </div>
                <div className="segmented-control" aria-label="Queue filter">
                  <button type="button">All</button>
                  <button type="button">Mine</button>
                  <button type="button">High</button>
                </div>
              </div>

              {isLoading && <p className="state-message">Loading dashboard...</p>}
              {error && <p className="state-message error">{error}</p>}
              {!isLoading && !error && filteredActions.length === 0 && (
                <p className="state-message">No open actions match this search.</p>
              )}
              {!isLoading && !error && filteredActions.length > 0 && (
                <div className="action-list">
                  {filteredActions.map((action) => (
                    <button
                      className={`action-row ${selectedAction?.id === action.id ? 'selected' : ''}`}
                      key={action.id}
                      onClick={() => setSelectedActionId(action.id)}
                      type="button"
                    >
                      <span className={`status ${normalizeBucket(action.bucket)}`}>
                        {action.bucket}
                      </span>
                      <span className="action-main">
                        <strong>{action.title}</strong>
                        <small>
                          {action.borrowerName} - {action.loanNumber} - {sectionCopy[action.section] ?? action.section}
                        </small>
                      </span>
                      <span className="action-meta">
                        <strong>{formatDueDate(action.dueDate)}</strong>
                        <small>{action.priority}</small>
                      </span>
                    </button>
                  ))}
                </div>
              )}
            </div>

            <LoanContextPanel
              action={selectedAction}
              notes={selectedNotes}
              noteDraft={noteDraft}
              onAddNote={addNote}
              onComplete={completeSelectedAction}
              onDraftChange={setNoteDraft}
              onReschedule={rescheduleSelectedAction}
              status={selectedAction ? actionStatus[selectedAction.id] : undefined}
            />
          </section>
        ) : (
          <section className="panel pipeline-panel">
            <div className="panel-header">
              <div>
                <h2>Pipeline snapshot</h2>
                <p>{pipelineLoans.length} active loans</p>
              </div>
            </div>
            <div className="pipeline-table" role="table" aria-label="Loan pipeline">
              <div className="pipeline-heading" role="row">
                <span>Borrower</span>
                <span>Loan</span>
                <span>Stage</span>
                <span>Next action</span>
                <span>Due</span>
              </div>
              {pipelineLoans.map((loan) => (
                <button
                  className="pipeline-row"
                  key={loan.loanNumber}
                  onClick={() => {
                    setView('dashboard')
                    setSelectedActionId(activeActions.find((action) => action.loanNumber === loan.loanNumber)?.id ?? null)
                  }}
                  role="row"
                  type="button"
                >
                  <span>{loan.borrowerName}</span>
                  <span>{loan.loanNumber}</span>
                  <span>{loan.stage}</span>
                  <span>{loan.nextAction}</span>
                  <span>{formatDueDate(loan.dueDate)}</span>
                </button>
              ))}
            </div>
          </section>
        )}
      </section>
    </main>
  )
}

function LoanContextPanel({
  action,
  notes,
  noteDraft,
  onAddNote,
  onComplete,
  onDraftChange,
  onReschedule,
  status,
}: {
  action: DashboardAction | null
  notes: string[]
  noteDraft: string
  onAddNote: () => void
  onComplete: () => void
  onDraftChange: (value: string) => void
  onReschedule: () => void
  status?: ActionStatus
}) {
  if (!action) {
    return (
      <aside className="panel detail-panel">
        <h2>Loan context</h2>
        <p className="state-message">Select an action to review the loan.</p>
      </aside>
    )
  }

  return (
    <aside className="panel detail-panel">
      <div className="detail-header">
        <span className={`status ${normalizeBucket(action.bucket)}`}>{action.bucket}</span>
        <h2>{action.borrowerName}</h2>
        <p>{action.loanNumber} - {sectionCopy[action.section] ?? action.section}</p>
      </div>

      <dl>
        <div>
          <dt>Next step</dt>
          <dd>{action.title}</dd>
        </div>
        <div>
          <dt>Due date</dt>
          <dd>{formatDueDate(action.dueDate)}</dd>
        </div>
        <div>
          <dt>Priority</dt>
          <dd>{action.priority}</dd>
        </div>
        <div>
          <dt>Prototype status</dt>
          <dd>{status === 'rescheduled' ? 'Rescheduled' : 'Ready for review'}</dd>
        </div>
      </dl>

      <div className="workflow-actions">
        <button type="button" onClick={onComplete}>Complete</button>
        <button className="secondary" type="button" onClick={onReschedule}>Reschedule</button>
      </div>

      <div className="note-box">
        <label htmlFor="noteDraft">File note</label>
        <textarea
          id="noteDraft"
          onChange={(event) => onDraftChange(event.target.value)}
          placeholder="Add a quick borrower, title, or realtor note"
          rows={4}
          value={noteDraft}
        />
        <button className="secondary" type="button" onClick={onAddNote}>Add note</button>
      </div>

      <div className="activity-feed">
        <h3>Recent activity</h3>
        {(notes.length > 0 ? notes : ['Loan opened from dashboard.', 'Awaiting next condition update.']).map((note, index) => (
          <p key={`${note}-${index}`}>{note}</p>
        ))}
      </div>
    </aside>
  )
}

export default App
