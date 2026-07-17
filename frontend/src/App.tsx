import { useEffect, useMemo, useState } from 'react'
import {
  addActionComment,
  completeAction,
  getDashboard,
  getLoan,
  getLoans,
  rescheduleAction,
} from './api'
import type {
  DashboardAction,
  DashboardSummary,
  LoanDetail,
  LoanListItem,
} from './api'
import './App.css'

type WorkspaceView = 'dashboard' | 'loans'
type QueueFilter = 'all' | 'overdue' | 'today' | 'high'

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

function formatDueDate(value: string | null) {
  if (!value) {
    return 'Not set'
  }

  const date = new Date(`${value}T00:00:00`)

  return new Intl.DateTimeFormat(undefined, {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  }).format(date)
}

function formatDateTime(value: string) {
  return new Intl.DateTimeFormat(undefined, {
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
  }).format(new Date(value))
}

function addDays(value: string, days: number) {
  const date = new Date(`${value}T00:00:00`)
  date.setDate(date.getDate() + days)

  return date.toISOString().slice(0, 10)
}

function normalizeBucket(bucket: string) {
  return bucket.toLowerCase().replace(' ', '-')
}

function App() {
  const [dashboard, setDashboard] = useState<DashboardSummary>(emptyDashboard)
  const [loans, setLoans] = useState<LoanListItem[]>([])
  const [loanDetail, setLoanDetail] = useState<LoanDetail | null>(null)
  const [searchTerm, setSearchTerm] = useState('')
  const [selectedActionId, setSelectedActionId] = useState<string | null>(null)
  const [view, setView] = useState<WorkspaceView>('dashboard')
  const [queueFilter, setQueueFilter] = useState<QueueFilter>('all')
  const [noteDraft, setNoteDraft] = useState('')
  const [rescheduleDate, setRescheduleDate] = useState('')
  const [rescheduleReason, setRescheduleReason] = useState('')
  const [isLoading, setIsLoading] = useState(true)
  const [isMutating, setIsMutating] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [workflowMessage, setWorkflowMessage] = useState<string | null>(null)

  async function loadWorkspace(preferredActionId?: string | null) {
    const [dashboardSummary, loanRows] = await Promise.all([
      getDashboard(),
      getLoans(),
    ])
    setDashboard(dashboardSummary)
    setLoans(loanRows)

    const nextSelectedActionId = preferredActionId
      && dashboardSummary.openActions.some((action) => action.id === preferredActionId)
      ? preferredActionId
      : dashboardSummary.openActions[0]?.id ?? null

    setSelectedActionId(nextSelectedActionId)
    return { dashboardSummary, nextSelectedActionId }
  }

  useEffect(() => {
    let isMounted = true

    async function loadInitialState() {
      try {
        const { dashboardSummary, nextSelectedActionId } = await loadWorkspace()
        const selectedAction = dashboardSummary.openActions.find((action) => action.id === nextSelectedActionId)

        if (selectedAction) {
          setLoanDetail(await getLoan(selectedAction.loanNumber))
        }

        if (isMounted) {
          setError(null)
        }
      } catch (caughtError) {
        if (isMounted) {
          setError(caughtError instanceof Error ? caughtError.message : 'Dashboard request failed')
        }
      } finally {
        if (isMounted) {
          setIsLoading(false)
        }
      }
    }

    void loadInitialState()

    return () => {
      isMounted = false
    }
  }, [])

  const filteredActions = useMemo(() => {
    const query = searchTerm.trim().toLowerCase()

    return dashboard.openActions.filter((action) => {
      const matchesFilter = queueFilter === 'all'
        || (queueFilter === 'overdue' && action.bucket === 'Overdue')
        || (queueFilter === 'today' && action.bucket === 'Due Today')
        || (queueFilter === 'high' && action.priority === 'High')

      if (!matchesFilter) {
        return false
      }

      if (!query) {
        return true
      }

      return [
        action.borrowerName,
        action.loanNumber,
        action.title,
        action.section,
        action.bucket,
        action.priority,
      ].some((value) => value.toLowerCase().includes(query))
    })
  }, [dashboard.openActions, queueFilter, searchTerm])

  const filteredLoans = useMemo(() => {
    const query = searchTerm.trim().toLowerCase()

    if (!query) {
      return loans
    }

    return loans.filter((loan) =>
      [
        loan.borrowerName,
        loan.loanNumber,
        loan.stage,
        loan.status,
        loan.priority,
        loan.nextActionTitle ?? '',
      ].some((value) => value.toLowerCase().includes(query)),
    )
  }, [loans, searchTerm])

  const selectedAction = useMemo(() => (
    filteredActions.find((action) => action.id === selectedActionId)
    ?? filteredActions[0]
    ?? null
  ), [filteredActions, selectedActionId])

  useEffect(() => {
    let isMounted = true

    async function loadLoanDetail() {
      if (!selectedAction) {
        setLoanDetail(null)
        return
      }

      try {
        const detail = await getLoan(selectedAction.loanNumber)

        if (isMounted) {
          setLoanDetail(detail)
        }
      } catch (caughtError) {
        if (isMounted) {
          setError(caughtError instanceof Error ? caughtError.message : 'Loan detail request failed')
        }
      }
    }

    void loadLoanDetail()

    return () => {
      isMounted = false
    }
  }, [selectedAction])

  useEffect(() => {
    if (!selectedAction) {
      setRescheduleDate('')
      setRescheduleReason('')
      return
    }

    setRescheduleDate(addDays(selectedAction.dueDate, 3))
    setRescheduleReason('')
  }, [selectedAction])

  async function runWorkflow(
    action: () => Promise<unknown>,
    message: string,
    preferredActionId: string | null | undefined = selectedAction?.id,
  ) {
    setIsMutating(true)
    setWorkflowMessage(null)
    setError(null)

    try {
      await action()
      const { dashboardSummary, nextSelectedActionId } = await loadWorkspace(preferredActionId)
      const nextAction = dashboardSummary.openActions.find((item) => item.id === nextSelectedActionId)

      if (nextAction) {
        setLoanDetail(await getLoan(nextAction.loanNumber))
      } else {
        setLoanDetail(null)
      }

      setWorkflowMessage(message)
    } catch (caughtError) {
      setError(caughtError instanceof Error ? caughtError.message : 'Workflow request failed')
    } finally {
      setIsMutating(false)
    }
  }

  function completeSelectedAction() {
    if (!selectedAction) {
      return
    }

    void runWorkflow(
      () => completeAction(selectedAction.id),
      `${selectedAction.id} completed.`,
      null,
    )
  }

  function rescheduleSelectedAction() {
    if (!selectedAction || !rescheduleDate || !rescheduleReason.trim()) {
      return
    }

    void runWorkflow(
      () => rescheduleAction(selectedAction.id, rescheduleDate, rescheduleReason.trim()),
      `${selectedAction.id} rescheduled.`,
      selectedAction.id,
    )
  }

  function addNote() {
    if (!selectedAction || !noteDraft.trim()) {
      return
    }

    const body = noteDraft.trim()
    setNoteDraft('')
    void runWorkflow(
      () => addActionComment(selectedAction.id, body),
      'Note added to loan file.',
      selectedAction.id,
    )
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
          <strong>{dashboard.overdueCount === 0 ? 'On track' : `${dashboard.overdueCount} overdue`}</strong>
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
            <strong>{dashboard.overdueCount}</strong>
            <small>Past business day</small>
          </article>
          <article className="metric-today">
            <span>Due today</span>
            <strong>{dashboard.dueTodayCount}</strong>
            <small>Needs officer touch</small>
          </article>
          <article className="metric-upcoming">
            <span>Upcoming</span>
            <strong>{dashboard.upcomingCount}</strong>
            <small>Within active queue</small>
          </article>
          <article>
            <span>Open queue</span>
            <strong>{dashboard.openActions.length}</strong>
            <small>Visible work items</small>
          </article>
        </section>

        {workflowMessage && <p className="state-message success">{workflowMessage}</p>}

        {view === 'dashboard' ? (
          <section className="content-grid">
            <div className="panel action-panel">
              <div className="panel-header">
                <div>
                  <h2>Open actions</h2>
                  <p>{filteredActions.length} visible</p>
                </div>
                <div className="segmented-control" aria-label="Queue filter">
                  <button className={queueFilter === 'all' ? 'active' : ''} type="button" onClick={() => setQueueFilter('all')}>
                    All
                  </button>
                  <button className={queueFilter === 'overdue' ? 'active' : ''} type="button" onClick={() => setQueueFilter('overdue')}>
                    Overdue
                  </button>
                  <button className={queueFilter === 'today' ? 'active' : ''} type="button" onClick={() => setQueueFilter('today')}>
                    Today
                  </button>
                  <button className={queueFilter === 'high' ? 'active' : ''} type="button" onClick={() => setQueueFilter('high')}>
                    High
                  </button>
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
              detail={loanDetail}
              disabled={isMutating}
              noteDraft={noteDraft}
              onAddNote={addNote}
              onComplete={completeSelectedAction}
              onDraftChange={setNoteDraft}
              onRescheduleDateChange={setRescheduleDate}
              onRescheduleReasonChange={setRescheduleReason}
              onReschedule={rescheduleSelectedAction}
              rescheduleDate={rescheduleDate}
              rescheduleReason={rescheduleReason}
            />
          </section>
        ) : (
          <section className="panel pipeline-panel">
            <div className="panel-header">
              <div>
                <h2>Pipeline snapshot</h2>
                <p>{filteredLoans.length} active loans</p>
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
              {filteredLoans.map((loan) => (
                <button
                  className="pipeline-row"
                  key={loan.loanNumber}
                  onClick={() => {
                    setView('dashboard')
                    setSelectedActionId(dashboard.openActions.find((action) => action.loanNumber === loan.loanNumber)?.id ?? null)
                  }}
                  role="row"
                  type="button"
                >
                  <span>{loan.borrowerName}</span>
                  <span>{loan.loanNumber}</span>
                  <span>{loan.stage}</span>
                  <span>{loan.nextActionTitle ?? 'No open action'}</span>
                  <span>{formatDueDate(loan.nextActionDueDate)}</span>
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
  detail,
  disabled,
  noteDraft,
  onAddNote,
  onComplete,
  onDraftChange,
  onRescheduleDateChange,
  onRescheduleReasonChange,
  onReschedule,
  rescheduleDate,
  rescheduleReason,
}: {
  action: DashboardAction | null
  detail: LoanDetail | null
  disabled: boolean
  noteDraft: string
  onAddNote: () => void
  onComplete: () => void
  onDraftChange: (value: string) => void
  onRescheduleDateChange: (value: string) => void
  onRescheduleReasonChange: (value: string) => void
  onReschedule: () => void
  rescheduleDate: string
  rescheduleReason: string
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
          <dt>Loan stage</dt>
          <dd>{detail?.stage ?? 'Loading'}</dd>
        </div>
        <div>
          <dt>Target close</dt>
          <dd>{formatDueDate(detail?.targetCloseDate ?? null)}</dd>
        </div>
        <div>
          <dt>Borrower email</dt>
          <dd>{detail?.borrowerEmail ?? 'Not available'}</dd>
        </div>
      </dl>

      <div className="workflow-actions">
        <button disabled={disabled} type="button" onClick={onComplete}>Complete</button>
      </div>

      <div className="reschedule-box">
        <label htmlFor="rescheduleDate">Reschedule</label>
        <div className="reschedule-controls">
          <input
            id="rescheduleDate"
            onChange={(event) => onRescheduleDateChange(event.target.value)}
            type="date"
            value={rescheduleDate}
          />
          <button
            className="secondary"
            disabled={disabled || !rescheduleDate || !rescheduleReason.trim()}
            type="button"
            onClick={onReschedule}
          >
            Save
          </button>
        </div>
        <textarea
          aria-label="Reschedule reason"
          onChange={(event) => onRescheduleReasonChange(event.target.value)}
          placeholder="Reason for changing the due date"
          rows={3}
          value={rescheduleReason}
        />
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
        <button className="secondary" disabled={disabled || !noteDraft.trim()} type="button" onClick={onAddNote}>Add note</button>
      </div>

      <div className="activity-feed">
        <h3>Recent notes</h3>
        {(detail?.notes.length ? detail.notes : [{ body: 'No notes yet.', createdAtUtc: new Date().toISOString() }]).map((note, index) => (
          <p key={`${note.createdAtUtc}-${index}`}>
            <strong>{formatDateTime(note.createdAtUtc)}</strong>
            <span>{note.body}</span>
          </p>
        ))}
      </div>

      <div className="activity-feed">
        <h3>History</h3>
        {(detail?.history.length ? detail.history : []).slice(0, 4).map((event) => (
          <p key={`${event.actionId}-${event.occurredAtUtc}`}>
            <strong>{formatDateTime(event.occurredAtUtc)}</strong>
            <span>{event.actionId}: {event.eventType}</span>
          </p>
        ))}
      </div>
    </aside>
  )
}

export default App
