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

const emptyDashboard: DashboardSummary = {
  overdueCount: 0,
  dueTodayCount: 0,
  upcomingCount: 0,
  openActions: [],
}

function formatDueDate(value: string) {
  const date = new Date(`${value}T00:00:00`)

  return new Intl.DateTimeFormat(undefined, {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  }).format(date)
}

function App() {
  const [dashboard, setDashboard] = useState<DashboardSummary>(emptyDashboard)
  const [searchTerm, setSearchTerm] = useState('')
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

        setDashboard(await response.json() as DashboardSummary)
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

  const filteredActions = useMemo(() => {
    const query = searchTerm.trim().toLowerCase()

    if (!query) {
      return dashboard.openActions
    }

    return dashboard.openActions.filter((action) =>
      [
        action.borrowerName,
        action.loanNumber,
        action.title,
        action.section,
        action.bucket,
        action.priority,
      ].some((value) => value.toLowerCase().includes(query)),
    )
  }, [dashboard.openActions, searchTerm])

  const selectedAction = filteredActions[0] ?? dashboard.openActions[0]

  return (
    <main className="app-shell">
      <aside className="sidebar" aria-label="Primary">
        <div className="brand">
          <span className="brand-mark">BA</span>
          <span>Broker App</span>
        </div>
        <nav>
          <a className="active" href="#dashboard">Dashboard</a>
          <a href="#customers">Customers</a>
          <a href="#loans">Loans</a>
          <a href="#actions">Actions</a>
          <a href="#reports">Reports</a>
        </nav>
      </aside>

      <section className="workspace" id="dashboard">
        <header className="topbar">
          <div>
            <p className="eyebrow">Daily workflow</p>
            <h1>Processing dashboard</h1>
          </div>
          <button type="button">New action</button>
        </header>

        <section className="metrics" aria-label="Action summary">
          <article>
            <span>Overdue</span>
            <strong>{dashboard.overdueCount}</strong>
          </article>
          <article>
            <span>Due today</span>
            <strong>{dashboard.dueTodayCount}</strong>
          </article>
          <article>
            <span>Upcoming</span>
            <strong>{dashboard.upcomingCount}</strong>
          </article>
        </section>

        <section className="content-grid">
          <div className="panel">
            <div className="panel-header">
              <h2>Open actions</h2>
              <input
                aria-label="Search actions"
                onChange={(event) => setSearchTerm(event.target.value)}
                placeholder="Search loans or borrowers"
                value={searchTerm}
              />
            </div>

            {isLoading && <p className="state-message">Loading dashboard...</p>}
            {error && <p className="state-message error">{error}</p>}
            {!isLoading && !error && filteredActions.length === 0 && (
              <p className="state-message">No open actions match this search.</p>
            )}
            {!isLoading && !error && filteredActions.length > 0 && (
              <div className="action-list">
                {filteredActions.map((action) => (
                  <article className="action-row" key={action.id}>
                    <div>
                      <span className={`status ${action.bucket.toLowerCase().replace(' ', '-')}`}>
                        {action.bucket}
                      </span>
                      <h3>{action.title}</h3>
                      <p>
                        {action.borrowerName} - {action.loanNumber} - {action.section} - {formatDueDate(action.dueDate)}
                      </p>
                    </div>
                    <span className="priority">{action.priority}</span>
                  </article>
                ))}
              </div>
            )}
          </div>

          <aside className="panel detail-panel">
            <h2>Selected loan</h2>
            {selectedAction ? (
              <dl>
                <div>
                  <dt>Borrower</dt>
                  <dd>{selectedAction.borrowerName}</dd>
                </div>
                <div>
                  <dt>Loan number</dt>
                  <dd>{selectedAction.loanNumber}</dd>
                </div>
                <div>
                  <dt>Next step</dt>
                  <dd>{selectedAction.title}</dd>
                </div>
                <div>
                  <dt>Due</dt>
                  <dd>{formatDueDate(selectedAction.dueDate)}</dd>
                </div>
              </dl>
            ) : (
              <p className="state-message">No loan selected.</p>
            )}
            <button type="button" className="secondary">View loan</button>
          </aside>
        </section>
      </section>
    </main>
  )
}

export default App
