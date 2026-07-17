import { useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import {
  addActionComment,
  completeAction,
  createFileIntake,
  createLoanAction,
  getCustomer,
  getCustomers,
  getDashboard,
  getLoan,
  getLoans,
  getReportSummary,
  rescheduleAction,
} from './api'
import type {
  DashboardAction,
  DashboardSummary,
  CustomerDetail,
  CustomerListItem,
  LoanDetail,
  LoanListItem,
  ReportSummary,
} from './api'
import './App.css'

type WorkspaceView = 'dashboard' | 'loans' | 'customers' | 'reports' | 'intake'
type QueueFilter = 'all' | 'overdue' | 'today' | 'high'

type IntakeActionForm = {
  title: string
  section: string
  priority: string
  dueDate: string
  description: string
}

type IntakeFormState = {
  firstName: string
  lastName: string
  email: string
  phone: string
  loanNumber: string
  type: string
  stage: string
  amount: string
  targetCloseDate: string
  actions: IntakeActionForm[]
  initialNote: string
}

type FollowUpActionForm = {
  title: string
  section: string
  priority: string
  dueDate: string
  description: string
}

const emptyDashboard: DashboardSummary = {
  overdueCount: 0,
  dueTodayCount: 0,
  upcomingCount: 0,
  openActions: [],
}

const emptyReportSummary: ReportSummary = {
  metrics: [],
  pipelineByStage: [],
  openActionsBySection: [],
  openActionsByPriority: [],
  upcomingClosings: [],
  oldestOpenActions: [],
}

const sectionCopy: Record<string, string> = {
  Borrower: 'Borrower conditions',
  Title: 'Title conditions',
  Realtor: 'Realtor follow-up',
}

const loanTypes = ['Purchase', 'Refinance', 'HELOC']
const loanStages = ['New file', 'Processing', 'Condition review', 'Clear to close']
const actionSections = ['Borrower', 'Title', 'Realtor']
const actionPriorities = ['Normal', 'High']

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

function emptyIntakeAction(): IntakeActionForm {
  return {
    title: '',
    section: 'Borrower',
    priority: 'Normal',
    dueDate: addDays(new Date().toISOString().slice(0, 10), 1),
    description: '',
  }
}

function emptyFollowUpAction(): FollowUpActionForm {
  return {
    title: '',
    section: 'Borrower',
    priority: 'Normal',
    dueDate: addDays(new Date().toISOString().slice(0, 10), 2),
    description: '',
  }
}

function emptyIntakeForm(): IntakeFormState {
  return {
    firstName: '',
    lastName: '',
    email: '',
    phone: '',
    loanNumber: '',
    type: 'Purchase',
    stage: 'New file',
    amount: '',
    targetCloseDate: '',
    actions: [emptyIntakeAction()],
    initialNote: '',
  }
}

function optionalText(value: string) {
  const trimmed = value.trim()

  return trimmed ? trimmed : null
}

function App() {
  const [dashboard, setDashboard] = useState<DashboardSummary>(emptyDashboard)
  const [loans, setLoans] = useState<LoanListItem[]>([])
  const [customers, setCustomers] = useState<CustomerListItem[]>([])
  const [reportSummary, setReportSummary] = useState<ReportSummary>(emptyReportSummary)
  const [loanDetail, setLoanDetail] = useState<LoanDetail | null>(null)
  const [customerDetail, setCustomerDetail] = useState<CustomerDetail | null>(null)
  const [searchTerm, setSearchTerm] = useState('')
  const [selectedActionId, setSelectedActionId] = useState<string | null>(null)
  const [selectedCustomerId, setSelectedCustomerId] = useState<string | null>(null)
  const [view, setView] = useState<WorkspaceView>('dashboard')
  const [queueFilter, setQueueFilter] = useState<QueueFilter>('all')
  const [noteDraft, setNoteDraft] = useState('')
  const [rescheduleDate, setRescheduleDate] = useState('')
  const [rescheduleReason, setRescheduleReason] = useState('')
  const [intakeForm, setIntakeForm] = useState<IntakeFormState>(() => emptyIntakeForm())
  const [followUpAction, setFollowUpAction] = useState<FollowUpActionForm>(() => emptyFollowUpAction())
  const [isLoading, setIsLoading] = useState(true)
  const [isMutating, setIsMutating] = useState(false)
  const [isSubmittingIntake, setIsSubmittingIntake] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [workflowMessage, setWorkflowMessage] = useState<string | null>(null)

  async function loadWorkspace(preferredActionId?: string | null) {
    const [dashboardSummary, loanRows, customerRows, reportRows] = await Promise.all([
      getDashboard(),
      getLoans(),
      getCustomers(),
      getReportSummary(),
    ])
    setDashboard(dashboardSummary)
    setLoans(loanRows)
    setCustomers(customerRows)
    setReportSummary(reportRows)

    const nextSelectedActionId = preferredActionId
      && dashboardSummary.openActions.some((action) => action.id === preferredActionId)
      ? preferredActionId
      : dashboardSummary.openActions[0]?.id ?? null

    setSelectedActionId(nextSelectedActionId)
    setSelectedCustomerId((current) => current && customerRows.some((customer) => customer.id === current)
      ? current
      : customerRows[0]?.id ?? null)
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

  const filteredCustomers = useMemo(() => {
    const query = searchTerm.trim().toLowerCase()

    if (!query) {
      return customers
    }

    return customers.filter((customer) =>
      [
        customer.borrowerName,
        customer.email ?? '',
        customer.phone ?? '',
        customer.status,
        customer.nextActionTitle ?? '',
      ].some((value) => value.toLowerCase().includes(query)),
    )
  }, [customers, searchTerm])

  const selectedAction = useMemo(() => (
    filteredActions.find((action) => action.id === selectedActionId)
    ?? filteredActions[0]
    ?? null
  ), [filteredActions, selectedActionId])

  const selectedCustomer = useMemo(() => (
    filteredCustomers.find((customer) => customer.id === selectedCustomerId)
    ?? filteredCustomers[0]
    ?? null
  ), [filteredCustomers, selectedCustomerId])

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
    let isMounted = true

    async function loadCustomerDetail() {
      if (view !== 'customers' || !selectedCustomer) {
        setCustomerDetail(null)
        return
      }

      try {
        const detail = await getCustomer(selectedCustomer.id)

        if (isMounted) {
          setCustomerDetail(detail)
        }
      } catch (caughtError) {
        if (isMounted) {
          setError(caughtError instanceof Error ? caughtError.message : 'Customer detail request failed')
        }
      }
    }

    void loadCustomerDetail()

    return () => {
      isMounted = false
    }
  }, [selectedCustomer, view])

  useEffect(() => {
    if (!selectedAction) {
      setRescheduleDate('')
      setRescheduleReason('')
      return
    }

    setRescheduleDate(addDays(selectedAction.dueDate, 3))
    setRescheduleReason('')
    setFollowUpAction(emptyFollowUpAction())
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

  function updateFollowUpAction(field: keyof FollowUpActionForm, value: string) {
    setFollowUpAction((current) => ({
      ...current,
      [field]: value,
    }))
  }

  function submitFollowUpAction() {
    if (!selectedAction || !followUpAction.title.trim() || !followUpAction.dueDate) {
      return
    }

    const loanNumber = selectedAction.loanNumber

    void (async () => {
      setIsMutating(true)
      setWorkflowMessage(null)
      setError(null)

      try {
        const response = await createLoanAction(loanNumber, {
          title: followUpAction.title.trim(),
          section: followUpAction.section,
          priority: followUpAction.priority,
          dueDate: followUpAction.dueDate,
          description: optionalText(followUpAction.description),
        })
        await loadWorkspace(response.id)
        setLoanDetail(await getLoan(response.loanNumber))
        setSelectedActionId(response.id)
        setFollowUpAction(emptyFollowUpAction())
        setWorkflowMessage(`Follow-up action ${response.id} added to ${loanNumber}.`)
      } catch (caughtError) {
        setError(caughtError instanceof Error ? caughtError.message : 'Follow-up action request failed')
      } finally {
        setIsMutating(false)
      }
    })()
  }

  function updateIntakeField(field: keyof Omit<IntakeFormState, 'actions'>, value: string) {
    setIntakeForm((current) => ({
      ...current,
      [field]: value,
    }))
  }

  function updateIntakeAction(index: number, field: keyof IntakeActionForm, value: string) {
    setIntakeForm((current) => ({
      ...current,
      actions: current.actions.map((action, actionIndex) => (
        actionIndex === index ? { ...action, [field]: value } : action
      )),
    }))
  }

  function addIntakeAction() {
    setIntakeForm((current) => current.actions.length >= 3
      ? current
      : {
          ...current,
          actions: [...current.actions, emptyIntakeAction()],
        })
  }

  function removeIntakeAction(index: number) {
    setIntakeForm((current) => current.actions.length <= 1
      ? current
      : {
          ...current,
          actions: current.actions.filter((_, actionIndex) => actionIndex !== index),
        })
  }

  async function submitIntake(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setIsSubmittingIntake(true)
    setWorkflowMessage(null)
    setError(null)

    try {
      const response = await createFileIntake({
        customer: {
          firstName: intakeForm.firstName.trim(),
          lastName: intakeForm.lastName.trim(),
          email: optionalText(intakeForm.email),
          phone: optionalText(intakeForm.phone),
        },
        loan: {
          loanNumber: intakeForm.loanNumber.trim(),
          type: intakeForm.type,
          stage: intakeForm.stage,
          amount: intakeForm.amount.trim() ? Number(intakeForm.amount) : null,
          targetCloseDate: optionalText(intakeForm.targetCloseDate),
        },
        actions: intakeForm.actions.map((action) => ({
          title: action.title.trim(),
          section: action.section,
          priority: action.priority,
          dueDate: action.dueDate,
          description: optionalText(action.description),
        })),
        initialNote: optionalText(intakeForm.initialNote),
      })

      await loadWorkspace(response.createdActionIds[0] ?? null)
      setLoanDetail(await getLoan(response.loanNumber))
      setSelectedActionId(response.createdActionIds[0] ?? null)
      setIntakeForm(emptyIntakeForm())
      setView('dashboard')
      setWorkflowMessage(
        `${response.loanNumber} created for ${response.borrowerName}${response.customerMatched ? ' using existing borrower.' : '.'}`,
      )
    } catch (caughtError) {
      setError(caughtError instanceof Error ? caughtError.message : 'Intake request failed')
    } finally {
      setIsSubmittingIntake(false)
    }
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
          <button className={view === 'customers' ? 'active' : ''} type="button" onClick={() => setView('customers')}>
            Customers
          </button>
          <button className={view === 'reports' ? 'active' : ''} type="button" onClick={() => setView('reports')}>
            Reports
          </button>
          <button disabled title="Coming soon" type="button">Admin</button>
        </nav>
        <div className="sidebar-summary">
          <span>Queue health</span>
          <strong>{dashboard.overdueCount === 0 ? 'On track' : `${dashboard.overdueCount} overdue`}</strong>
        </div>
      </aside>

      <section className="workspace" id="dashboard">
        <header className="topbar">
          <div>
            <p className="eyebrow">{view === 'intake' ? 'File intake' : 'Daily workflow'}</p>
            <h1>
              {view === 'dashboard' && 'Processing dashboard'}
              {view === 'loans' && 'Loan pipeline'}
              {view === 'customers' && 'Customers'}
              {view === 'reports' && 'Reports'}
              {view === 'intake' && 'New file intake'}
            </h1>
          </div>
          <div className="topbar-actions">
            {view === 'intake' ? (
              <button className="secondary" type="button" onClick={() => setView('dashboard')}>Cancel intake</button>
            ) : (
              <>
                <input
                  aria-label="Search loans and actions"
                  onChange={(event) => setSearchTerm(event.target.value)}
                  placeholder="Search loans or borrowers"
                  value={searchTerm}
                />
                <button type="button" onClick={() => setView('intake')}>New action</button>
              </>
            )}
          </div>
        </header>

        {view !== 'intake' && (
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
        )}

        {workflowMessage && <p className="state-message success">{workflowMessage}</p>}

        {view === 'intake' ? (
          <IntakePage
            disabled={isSubmittingIntake}
            form={intakeForm}
            onAddAction={addIntakeAction}
            onRemoveAction={removeIntakeAction}
            onSubmit={submitIntake}
            onUpdateAction={updateIntakeAction}
            onUpdateField={updateIntakeField}
          />
        ) : view === 'dashboard' ? (
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
              followUpAction={followUpAction}
              noteDraft={noteDraft}
              onAddNote={addNote}
              onComplete={completeSelectedAction}
              onCreateFollowUpAction={submitFollowUpAction}
              onDraftChange={setNoteDraft}
              onFollowUpActionChange={updateFollowUpAction}
              onRescheduleDateChange={setRescheduleDate}
              onRescheduleReasonChange={setRescheduleReason}
              onReschedule={rescheduleSelectedAction}
              rescheduleDate={rescheduleDate}
              rescheduleReason={rescheduleReason}
            />
          </section>
        ) : view === 'customers' ? (
          <section className="content-grid">
            <div className="panel customer-panel">
              <div className="panel-header">
                <div>
                  <h2>Borrower directory</h2>
                  <p>{filteredCustomers.length} visible</p>
                </div>
              </div>
              {isLoading && <p className="state-message">Loading customers...</p>}
              {error && <p className="state-message error">{error}</p>}
              {!isLoading && !error && filteredCustomers.length === 0 && (
                <p className="state-message">No customers match this search.</p>
              )}
              {!isLoading && !error && filteredCustomers.length > 0 && (
                <div className="customer-list">
                  {filteredCustomers.map((customer) => (
                    <button
                      className={`customer-row ${selectedCustomer?.id === customer.id ? 'selected' : ''}`}
                      key={customer.id}
                      onClick={() => setSelectedCustomerId(customer.id)}
                      type="button"
                    >
                      <span className="customer-main">
                        <strong>{customer.borrowerName}</strong>
                        <small>{customer.email ?? 'No email'} - {customer.phone ?? 'No phone'}</small>
                      </span>
                      <span className="customer-stat">
                        <strong>{customer.loanCount}</strong>
                        <small>Loans</small>
                      </span>
                      <span className="customer-stat">
                        <strong>{customer.openActionCount}</strong>
                        <small>Open</small>
                      </span>
                      <span className="customer-next">
                        <strong>{customer.nextActionTitle ?? 'No open action'}</strong>
                        <small>{formatDueDate(customer.nextActionDueDate)}</small>
                      </span>
                    </button>
                  ))}
                </div>
              )}
            </div>

            <CustomerContextPanel
              detail={customerDetail}
              onOpenAction={(actionId) => {
                setView('dashboard')
                setSelectedActionId(actionId)
              }}
              onOpenLoan={(loanNumber) => {
                setView('dashboard')
                setSelectedActionId(dashboard.openActions.find((action) => action.loanNumber === loanNumber)?.id ?? null)
              }}
              selected={selectedCustomer}
            />
          </section>
        ) : view === 'reports' ? (
          <ReportsPage
            onOpenAction={(actionId) => {
              setView('dashboard')
              setSelectedActionId(actionId)
            }}
            onOpenLoan={(loanNumber) => {
              setView('dashboard')
              setSelectedActionId(dashboard.openActions.find((action) => action.loanNumber === loanNumber)?.id ?? null)
            }}
            summary={reportSummary}
          />
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

function ReportsPage({
  onOpenAction,
  onOpenLoan,
  summary,
}: {
  onOpenAction: (actionId: string) => void
  onOpenLoan: (loanNumber: string) => void
  summary: ReportSummary
}) {
  return (
    <section className="reports-page">
      <div className="report-metrics" aria-label="Report metrics">
        {summary.metrics.map((metric) => (
          <article className="report-metric" key={metric.label}>
            <span>{metric.label}</span>
            <strong>{metric.value}</strong>
          </article>
        ))}
      </div>

      <div className="report-grid">
        <BreakdownPanel title="Pipeline by stage" items={summary.pipelineByStage} />
        <BreakdownPanel title="Open actions by section" items={summary.openActionsBySection} />
        <BreakdownPanel title="Open actions by priority" items={summary.openActionsByPriority} />
      </div>

      <div className="report-grid two-panel">
        <section className="panel report-panel">
          <div className="panel-header">
            <div>
              <h2>Upcoming closings</h2>
              <p>{summary.upcomingClosings.length} files</p>
            </div>
          </div>
          <div className="report-list">
            {summary.upcomingClosings.map((loan) => (
              <button className="report-row" key={loan.loanNumber} type="button" onClick={() => onOpenLoan(loan.loanNumber)}>
                <span>
                  <strong>{loan.borrowerName}</strong>
                  <small>{loan.loanNumber} - {loan.stage}</small>
                </span>
                <span>
                  <strong>{formatDueDate(loan.targetCloseDate)}</strong>
                  <small>{loan.openActionCount} open</small>
                </span>
              </button>
            ))}
            {summary.upcomingClosings.length === 0 && <p className="state-message">No upcoming closings.</p>}
          </div>
        </section>

        <section className="panel report-panel">
          <div className="panel-header">
            <div>
              <h2>Oldest open actions</h2>
              <p>{summary.oldestOpenActions.length} actions</p>
            </div>
          </div>
          <div className="report-list">
            {summary.oldestOpenActions.map((action) => (
              <button className="report-row" key={action.id} type="button" onClick={() => onOpenAction(action.id)}>
                <span>
                  <strong>{action.title}</strong>
                  <small>{action.borrowerName} - {action.loanNumber}</small>
                </span>
                <span>
                  <strong>{formatDueDate(action.dueDate)}</strong>
                  <small>{action.daysOpen} days open</small>
                </span>
              </button>
            ))}
            {summary.oldestOpenActions.length === 0 && <p className="state-message">No open actions.</p>}
          </div>
        </section>
      </div>
    </section>
  )
}

function BreakdownPanel({
  items,
  title,
}: {
  items: Array<{ label: string; value: number }>
  title: string
}) {
  const maxValue = Math.max(...items.map((item) => item.value), 1)

  return (
    <section className="panel report-panel">
      <div className="panel-header">
        <div>
          <h2>{title}</h2>
          <p>{items.reduce((total, item) => total + item.value, 0)} total</p>
        </div>
      </div>
      <div className="breakdown-list">
        {items.map((item) => (
          <div className="breakdown-row" key={item.label}>
            <span>{item.label}</span>
            <strong>{item.value}</strong>
            <div aria-hidden="true">
              <i style={{ width: `${Math.max(8, (item.value / maxValue) * 100)}%` }} />
            </div>
          </div>
        ))}
        {items.length === 0 && <p className="state-message">No data yet.</p>}
      </div>
    </section>
  )
}

function CustomerContextPanel({
  detail,
  onOpenAction,
  onOpenLoan,
  selected,
}: {
  detail: CustomerDetail | null
  onOpenAction: (actionId: string) => void
  onOpenLoan: (loanNumber: string) => void
  selected: CustomerListItem | null
}) {
  if (!selected) {
    return (
      <aside className="panel detail-panel">
        <h2>Customer context</h2>
        <p className="state-message">Select a customer to review borrower activity.</p>
      </aside>
    )
  }

  return (
    <aside className="panel detail-panel customer-detail-panel">
      <div className="detail-header">
        <span className="status upcoming">{selected.status}</span>
        <h2>{selected.borrowerName}</h2>
        <p>{selected.email ?? 'No email'} - {selected.phone ?? 'No phone'}</p>
      </div>

      <dl>
        <div>
          <dt>Loans</dt>
          <dd>{detail?.loans.length ?? selected.loanCount}</dd>
        </div>
        <div>
          <dt>Open actions</dt>
          <dd>{detail?.openActions.length ?? selected.openActionCount}</dd>
        </div>
        <div>
          <dt>Next action</dt>
          <dd>{selected.nextActionTitle ?? 'None'}</dd>
        </div>
      </dl>

      <div className="activity-feed">
        <h3>Loans</h3>
        {(detail?.loans.length ? detail.loans : []).map((loan) => (
          <button className="context-row" key={loan.loanNumber} type="button" onClick={() => onOpenLoan(loan.loanNumber)}>
            <span>
              <strong>{loan.loanNumber}</strong>
              <small>{loan.type} - {loan.stage}</small>
            </span>
            <span>
              <strong>{loan.openActionCount}</strong>
              <small>Open</small>
            </span>
          </button>
        ))}
        {detail && detail.loans.length === 0 && <p>No loans yet.</p>}
      </div>

      <div className="activity-feed">
        <h3>Open actions</h3>
        {(detail?.openActions.length ? detail.openActions : []).slice(0, 6).map((action) => (
          <button className="context-row" key={action.id} type="button" onClick={() => onOpenAction(action.id)}>
            <span>
              <strong>{action.title}</strong>
              <small>{action.loanNumber} - {sectionCopy[action.section] ?? action.section}</small>
            </span>
            <span>
              <strong>{formatDueDate(action.dueDate)}</strong>
              <small>{action.priority}</small>
            </span>
          </button>
        ))}
        {detail && detail.openActions.length === 0 && <p>No open actions.</p>}
      </div>
    </aside>
  )
}

function IntakePage({
  disabled,
  form,
  onAddAction,
  onRemoveAction,
  onSubmit,
  onUpdateAction,
  onUpdateField,
}: {
  disabled: boolean
  form: IntakeFormState
  onAddAction: () => void
  onRemoveAction: (index: number) => void
  onSubmit: (event: FormEvent<HTMLFormElement>) => void
  onUpdateAction: (index: number, field: keyof IntakeActionForm, value: string) => void
  onUpdateField: (field: keyof Omit<IntakeFormState, 'actions'>, value: string) => void
}) {
  return (
    <form className="intake-page" onSubmit={onSubmit}>
      <section className="panel intake-section">
        <div className="panel-header">
          <div>
            <h2>Borrower</h2>
            <p>{form.email.trim() ? 'Email match will reuse an active customer' : 'New customer'}</p>
          </div>
        </div>
        <div className="form-grid two-column">
          <label>
            First name
            <input
              disabled={disabled}
              onChange={(event) => onUpdateField('firstName', event.target.value)}
              required
              value={form.firstName}
            />
          </label>
          <label>
            Last name
            <input
              disabled={disabled}
              onChange={(event) => onUpdateField('lastName', event.target.value)}
              required
              value={form.lastName}
            />
          </label>
          <label>
            Email
            <input
              disabled={disabled}
              onChange={(event) => onUpdateField('email', event.target.value)}
              type="email"
              value={form.email}
            />
          </label>
          <label>
            Phone
            <input
              disabled={disabled}
              onChange={(event) => onUpdateField('phone', event.target.value)}
              value={form.phone}
            />
          </label>
        </div>
      </section>

      <section className="panel intake-section">
        <div className="panel-header">
          <div>
            <h2>Loan</h2>
            <p>{form.loanNumber.trim() || 'Manual loan number'}</p>
          </div>
        </div>
        <div className="form-grid three-column">
          <label>
            Loan number
            <input
              disabled={disabled}
              onChange={(event) => onUpdateField('loanNumber', event.target.value)}
              required
              value={form.loanNumber}
            />
          </label>
          <label>
            Type
            <select disabled={disabled} onChange={(event) => onUpdateField('type', event.target.value)} value={form.type}>
              {loanTypes.map((loanType) => <option key={loanType}>{loanType}</option>)}
            </select>
          </label>
          <label>
            Stage
            <select disabled={disabled} onChange={(event) => onUpdateField('stage', event.target.value)} value={form.stage}>
              {loanStages.map((stage) => <option key={stage}>{stage}</option>)}
            </select>
          </label>
          <label>
            Amount
            <input
              disabled={disabled}
              min="0"
              onChange={(event) => onUpdateField('amount', event.target.value)}
              step="1000"
              type="number"
              value={form.amount}
            />
          </label>
          <label>
            Target close
            <input
              disabled={disabled}
              onChange={(event) => onUpdateField('targetCloseDate', event.target.value)}
              type="date"
              value={form.targetCloseDate}
            />
          </label>
        </div>
      </section>

      <section className="panel intake-section">
        <div className="panel-header">
          <div>
            <h2>Initial actions</h2>
            <p>{form.actions.length} of 3</p>
          </div>
          <button className="secondary" disabled={disabled || form.actions.length >= 3} type="button" onClick={onAddAction}>
            Add action
          </button>
        </div>
        <div className="intake-actions">
          {form.actions.map((action, index) => (
            <div className="intake-action" key={index}>
              <div className="intake-action-header">
                <strong>Action {index + 1}</strong>
                <button
                  className="ghost"
                  disabled={disabled || form.actions.length === 1}
                  type="button"
                  onClick={() => onRemoveAction(index)}
                >
                  Remove
                </button>
              </div>
              <div className="form-grid action-grid">
                <label>
                  Title
                  <input
                    disabled={disabled}
                    onChange={(event) => onUpdateAction(index, 'title', event.target.value)}
                    required
                    value={action.title}
                  />
                </label>
                <label>
                  Section
                  <select disabled={disabled} onChange={(event) => onUpdateAction(index, 'section', event.target.value)} value={action.section}>
                    {actionSections.map((section) => <option key={section}>{section}</option>)}
                  </select>
                </label>
                <label>
                  Priority
                  <select disabled={disabled} onChange={(event) => onUpdateAction(index, 'priority', event.target.value)} value={action.priority}>
                    {actionPriorities.map((priority) => <option key={priority}>{priority}</option>)}
                  </select>
                </label>
                <label>
                  Due date
                  <input
                    disabled={disabled}
                    onChange={(event) => onUpdateAction(index, 'dueDate', event.target.value)}
                    required
                    type="date"
                    value={action.dueDate}
                  />
                </label>
                <label className="span-all">
                  Description
                  <textarea
                    disabled={disabled}
                    onChange={(event) => onUpdateAction(index, 'description', event.target.value)}
                    rows={3}
                    value={action.description}
                  />
                </label>
              </div>
            </div>
          ))}
        </div>
      </section>

      <section className="panel intake-section">
        <div className="panel-header">
          <div>
            <h2>Initial note</h2>
            <p>Optional</p>
          </div>
        </div>
        <label className="full-width-label">
          File note
          <textarea
            disabled={disabled}
            onChange={(event) => onUpdateField('initialNote', event.target.value)}
            rows={4}
            value={form.initialNote}
          />
        </label>
      </section>

      <div className="intake-submit">
        <button disabled={disabled} type="submit">{disabled ? 'Creating file...' : 'Create file'}</button>
      </div>
    </form>
  )
}

function LoanContextPanel({
  action,
  detail,
  disabled,
  followUpAction,
  noteDraft,
  onAddNote,
  onComplete,
  onCreateFollowUpAction,
  onDraftChange,
  onFollowUpActionChange,
  onRescheduleDateChange,
  onRescheduleReasonChange,
  onReschedule,
  rescheduleDate,
  rescheduleReason,
}: {
  action: DashboardAction | null
  detail: LoanDetail | null
  disabled: boolean
  followUpAction: FollowUpActionForm
  noteDraft: string
  onAddNote: () => void
  onComplete: () => void
  onCreateFollowUpAction: () => void
  onDraftChange: (value: string) => void
  onFollowUpActionChange: (field: keyof FollowUpActionForm, value: string) => void
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

      <div className="follow-up-box">
        <div>
          <h3>New follow-up</h3>
          <p>Add another condition or partner touch to this loan.</p>
        </div>
        <input
          aria-label="Follow-up title"
          disabled={disabled}
          onChange={(event) => onFollowUpActionChange('title', event.target.value)}
          placeholder="Action title"
          value={followUpAction.title}
        />
        <div className="follow-up-grid">
          <select
            aria-label="Follow-up section"
            disabled={disabled}
            onChange={(event) => onFollowUpActionChange('section', event.target.value)}
            value={followUpAction.section}
          >
            {actionSections.map((section) => <option key={section}>{section}</option>)}
          </select>
          <select
            aria-label="Follow-up priority"
            disabled={disabled}
            onChange={(event) => onFollowUpActionChange('priority', event.target.value)}
            value={followUpAction.priority}
          >
            {actionPriorities.map((priority) => <option key={priority}>{priority}</option>)}
          </select>
          <input
            aria-label="Follow-up due date"
            disabled={disabled}
            onChange={(event) => onFollowUpActionChange('dueDate', event.target.value)}
            type="date"
            value={followUpAction.dueDate}
          />
        </div>
        <textarea
          aria-label="Follow-up description"
          disabled={disabled}
          onChange={(event) => onFollowUpActionChange('description', event.target.value)}
          placeholder="Optional context"
          rows={3}
          value={followUpAction.description}
        />
        <button
          className="secondary"
          disabled={disabled || !followUpAction.title.trim() || !followUpAction.dueDate}
          type="button"
          onClick={onCreateFollowUpAction}
        >
          Add follow-up
        </button>
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
