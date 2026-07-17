import { useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import {
  addActionComment,
  cancelAction,
  completeAction,
  createActionTemplate,
  createCustomerLoan,
  createFileIntake,
  createLoanAction,
  generateLoanActions,
  getActionTemplate,
  getActionEmailDraft,
  getActionTemplates,
  getCustomer,
  getCustomers,
  getCurrentUser,
  getDashboard,
  getLoan,
  getLoans,
  getReportSummary,
  getUsers,
  reassignAction,
  rescheduleAction,
  updateActionTemplate,
  updateCustomer,
  updateLoan,
} from './api'
import type {
  ActionTemplateDetail,
  ActionTemplateListItem,
  ActionEmailDraft,
  CurrentUser,
  DashboardAction,
  DashboardLoanAlert,
  DashboardSummary,
  CustomerDetail,
  CustomerListItem,
  LoanActionDetail,
  LoanDetail,
  LoanListItem,
  ReportSummary,
  UpsertActionTemplateRequest,
  UserListItem,
} from './api'
import './App.css'

type WorkspaceView = 'dashboard' | 'loans' | 'customers' | 'reports' | 'admin' | 'account' | 'intake'
type QueueFilter = 'all' | 'overdue' | 'today' | 'high'
type BorrowerMode = 'new' | 'existing'

type IntakeActionForm = {
  title: string
  section: string
  priority: string
  dueDate: string
  description: string
}

type IntakeFormState = {
  borrowerMode: BorrowerMode
  existingCustomerId: string
  firstName: string
  lastName: string
  email: string
  phone: string
  loanNumber: string
  type: string
  stage: string
  amount: string
  targetCloseDate: string
  coBorrowerEmail: string
  titleContactName: string
  titleContactEmail: string
  realtorName: string
  realtorEmail: string
  icdSent: boolean
  icdSigned: boolean
  lastContactDate: string
  actions: IntakeActionForm[]
  initialNote: string
  templateId: string
}

type FollowUpActionForm = {
  title: string
  section: string
  priority: string
  dueDate: string
  description: string
}

type CustomerEditForm = {
  firstName: string
  lastName: string
  email: string
  phone: string
  status: string
}

type LoanEditForm = {
  type: string
  stage: string
  status: string
  amount: string
  targetCloseDate: string
  coBorrowerEmail: string
  titleContactName: string
  titleContactEmail: string
  realtorName: string
  realtorEmail: string
  icdSent: boolean
  icdSigned: boolean
  lastContactDate: string
}

type TemplateItemForm = {
  sortOrder: string
  section: string
  title: string
  description: string
  priority: string
  dueOffsetDays: string
}

type TemplateFormState = {
  id: string | null
  name: string
  loanType: string
  stage: string
  isActive: boolean
  items: TemplateItemForm[]
}

const emptyDashboard: DashboardSummary = {
  overdueCount: 0,
  dueTodayCount: 0,
  upcomingCount: 0,
  closingWithin7DaysCount: 0,
  icdNotSentOrSignedCount: 0,
  closingWithin7Days: [],
  icdNeedsAttention: [],
  openActions: [],
}

const emptyReportSummary: ReportSummary = {
  metrics: [],
  pipelineByStage: [],
  openActionsBySection: [],
  openActionsByPriority: [],
  upcomingClosings: [],
  oldestOpenActions: [],
  recentActivity: [],
}

const sectionCopy: Record<string, string> = {
  Borrower: 'Borrower conditions',
  Title: 'Title conditions',
  Realtor: 'Realtor follow-up',
}

const loanTypes = ['Purchase', 'Refinance', 'HELOC']
const loanStages = ['New file', 'Processing', 'Condition review', 'Clear to close']
const loanStatuses = ['Draft', 'Active', 'On Hold', 'Closed', 'Canceled']
const customerStatuses = ['Active', 'Archived']
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

function formatDaysToClose(value: number | null) {
  if (value == null) {
    return 'No close date'
  }

  if (value === 0) {
    return 'Closes today'
  }

  return value > 0 ? `${value} days to close` : `${Math.abs(value)} days past close`
}

function formatIcdStatus(icdSent: boolean, icdSigned: boolean) {
  if (icdSent && icdSigned) {
    return 'Sent and signed'
  }

  if (icdSent) {
    return 'Sent, not signed'
  }

  return 'Not sent'
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

function emptyCustomerEditForm(): CustomerEditForm {
  return {
    firstName: '',
    lastName: '',
    email: '',
    phone: '',
    status: 'Active',
  }
}

function emptyLoanEditForm(): LoanEditForm {
  return {
    type: 'Purchase',
    stage: 'New file',
    status: 'Active',
    amount: '',
    targetCloseDate: '',
    coBorrowerEmail: '',
    titleContactName: '',
    titleContactEmail: '',
    realtorName: '',
    realtorEmail: '',
    icdSent: false,
    icdSigned: false,
    lastContactDate: '',
  }
}

function emptyIntakeForm(): IntakeFormState {
  return {
    borrowerMode: 'new',
    existingCustomerId: '',
    firstName: '',
    lastName: '',
    email: '',
    phone: '',
    loanNumber: '',
    type: 'Purchase',
    stage: 'New file',
    amount: '',
    targetCloseDate: '',
    coBorrowerEmail: '',
    titleContactName: '',
    titleContactEmail: '',
    realtorName: '',
    realtorEmail: '',
    icdSent: false,
    icdSigned: false,
    lastContactDate: '',
    actions: [emptyIntakeAction()],
    initialNote: '',
    templateId: '',
  }
}

function emptyTemplateItem(sortOrder = 1): TemplateItemForm {
  return {
    sortOrder: String(sortOrder),
    section: 'Borrower',
    title: '',
    description: '',
    priority: 'Normal',
    dueOffsetDays: String(sortOrder),
  }
}

function emptyTemplateForm(): TemplateFormState {
  return {
    id: null,
    name: '',
    loanType: 'Purchase',
    stage: 'New file',
    isActive: true,
    items: [
      emptyTemplateItem(1),
      { ...emptyTemplateItem(2), section: 'Title', title: '' },
      { ...emptyTemplateItem(3), section: 'Realtor', title: '' },
    ],
  }
}

function templateDetailToForm(template: ActionTemplateDetail): TemplateFormState {
  return {
    id: template.id,
    name: template.name,
    loanType: template.loanType,
    stage: template.stage,
    isActive: template.isActive,
    items: template.items.map((item) => ({
      sortOrder: String(item.sortOrder),
      section: item.section,
      title: item.title,
      description: item.description ?? '',
      priority: item.priority,
      dueOffsetDays: String(item.dueOffsetDays),
    })),
  }
}

function optionalText(value: string) {
  const trimmed = value.trim()

  return trimmed ? trimmed : null
}

function buildCustomerLoanRequest(form: IntakeFormState) {
  return {
    loan: {
      loanNumber: form.loanNumber.trim(),
      type: form.type,
      stage: form.stage,
      amount: form.amount.trim() ? Number(form.amount) : null,
      targetCloseDate: optionalText(form.targetCloseDate),
      coBorrowerEmail: optionalText(form.coBorrowerEmail),
      titleContactName: optionalText(form.titleContactName),
      titleContactEmail: optionalText(form.titleContactEmail),
      realtorName: optionalText(form.realtorName),
      realtorEmail: optionalText(form.realtorEmail),
      icdSent: form.icdSent,
      icdSigned: form.icdSigned,
      lastContactDate: optionalText(form.lastContactDate),
    },
    actions: form.templateId ? [] : form.actions.map((action) => ({
      title: action.title.trim(),
      section: action.section,
      priority: action.priority,
      dueDate: action.dueDate,
      description: optionalText(action.description),
    })),
    initialNote: optionalText(form.initialNote),
    templateId: optionalText(form.templateId),
  }
}

function App() {
  const [dashboard, setDashboard] = useState<DashboardSummary>(emptyDashboard)
  const [loans, setLoans] = useState<LoanListItem[]>([])
  const [customers, setCustomers] = useState<CustomerListItem[]>([])
  const [reportSummary, setReportSummary] = useState<ReportSummary>(emptyReportSummary)
  const [templates, setTemplates] = useState<ActionTemplateListItem[]>([])
  const [users, setUsers] = useState<UserListItem[]>([])
  const [currentUser, setCurrentUser] = useState<CurrentUser | null>(null)
  const [loanDetail, setLoanDetail] = useState<LoanDetail | null>(null)
  const [customerDetail, setCustomerDetail] = useState<CustomerDetail | null>(null)
  const [templateDetail, setTemplateDetail] = useState<ActionTemplateDetail | null>(null)
  const [searchTerm, setSearchTerm] = useState('')
  const [selectedActionId, setSelectedActionId] = useState<string | null>(null)
  const [selectedCustomerId, setSelectedCustomerId] = useState<string | null>(null)
  const [selectedLoanNumber, setSelectedLoanNumber] = useState<string | null>(null)
  const [selectedTemplateId, setSelectedTemplateId] = useState<string | null>(null)
  const [view, setView] = useState<WorkspaceView>('dashboard')
  const [queueFilter, setQueueFilter] = useState<QueueFilter>('all')
  const [noteDraft, setNoteDraft] = useState('')
  const [rescheduleDate, setRescheduleDate] = useState('')
  const [rescheduleReason, setRescheduleReason] = useState('')
  const [cancelReason, setCancelReason] = useState('')
  const [reassignUserId, setReassignUserId] = useState('')
  const [reassignReason, setReassignReason] = useState('')
  const [intakeForm, setIntakeForm] = useState<IntakeFormState>(() => emptyIntakeForm())
  const [customerLoanForm, setCustomerLoanForm] = useState<IntakeFormState>(() => emptyIntakeForm())
  const [followUpAction, setFollowUpAction] = useState<FollowUpActionForm>(() => emptyFollowUpAction())
  const [templateForm, setTemplateForm] = useState<TemplateFormState>(() => emptyTemplateForm())
  const [customerEditForm, setCustomerEditForm] = useState<CustomerEditForm>(() => emptyCustomerEditForm())
  const [loanEditForm, setLoanEditForm] = useState<LoanEditForm>(() => emptyLoanEditForm())
  const [emailDraft, setEmailDraft] = useState<ActionEmailDraft | null>(null)
  const [isEmailSendConfirmOpen, setIsEmailSendConfirmOpen] = useState(false)
  const [isLoading, setIsLoading] = useState(true)
  const [isMutating, setIsMutating] = useState(false)
  const [isGeneratingEmailDraft, setIsGeneratingEmailDraft] = useState(false)
  const [isSubmittingIntake, setIsSubmittingIntake] = useState(false)
  const [isSubmittingCustomerLoan, setIsSubmittingCustomerLoan] = useState(false)
  const [isSavingTemplate, setIsSavingTemplate] = useState(false)
  const [isSavingCustomer, setIsSavingCustomer] = useState(false)
  const [isSavingLoan, setIsSavingLoan] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [workflowMessage, setWorkflowMessage] = useState<string | null>(null)

  async function loadWorkspace(preferredActionId?: string | null) {
    const [dashboardSummary, loanRows, customerRows, reportRows, templateRows, userRows, activeUser] = await Promise.all([
      getDashboard(),
      getLoans(),
      getCustomers(),
      getReportSummary(),
      getActionTemplates(),
      getUsers(),
      getCurrentUser(),
    ])
    setDashboard(dashboardSummary)
    setLoans(loanRows)
    setCustomers(customerRows)
    setReportSummary(reportRows)
    setTemplates(templateRows)
    setUsers(userRows)
    setCurrentUser(activeUser)

    const nextSelectedActionId = preferredActionId
      && dashboardSummary.openActions.some((action) => action.id === preferredActionId)
      ? preferredActionId
      : dashboardSummary.openActions[0]?.id ?? null

    setSelectedActionId(nextSelectedActionId)
    setSelectedCustomerId((current) => current && customerRows.some((customer) => customer.id === current)
      ? current
      : customerRows[0]?.id ?? null)
    setSelectedLoanNumber((current) => current && loanRows.some((loan) => loan.loanNumber === current)
      ? current
      : loanRows[0]?.loanNumber ?? null)
    setSelectedTemplateId((current) => current && templateRows.some((template) => template.id === current)
      ? current
      : templateRows[0]?.id ?? null)
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

  const filteredTemplates = useMemo(() => {
    const query = searchTerm.trim().toLowerCase()

    if (!query) {
      return templates
    }

    return templates.filter((template) =>
      [
        template.name,
        template.loanType,
        template.stage,
        template.isActive ? 'active' : 'inactive',
      ].some((value) => value.toLowerCase().includes(query)),
    )
  }, [templates, searchTerm])

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

  const selectedLoan = useMemo(() => (
    filteredLoans.find((loan) => loan.loanNumber === selectedLoanNumber)
    ?? filteredLoans[0]
    ?? null
  ), [filteredLoans, selectedLoanNumber])

  const selectedTemplate = useMemo(() => (
    filteredTemplates.find((template) => template.id === selectedTemplateId)
    ?? filteredTemplates[0]
    ?? null
  ), [filteredTemplates, selectedTemplateId])

  useEffect(() => {
    setEmailDraft(null)
    setIsEmailSendConfirmOpen(false)
  }, [selectedAction?.id])

  useEffect(() => {
    if (view !== 'loans') {
      return
    }

    setSelectedLoanNumber((current) => current && filteredLoans.some((loan) => loan.loanNumber === current)
      ? current
      : filteredLoans[0]?.loanNumber ?? null)
  }, [filteredLoans, view])

  useEffect(() => {
    setIntakeForm((current) => current.existingCustomerId || customers.length === 0
      ? current
      : { ...current, existingCustomerId: customers[0].id })
  }, [customers])

  useEffect(() => {
    setCustomerLoanForm(emptyIntakeForm())
  }, [selectedCustomer?.id])

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

    async function loadSelectedLoanDetail() {
      if (view !== 'loans' || !selectedLoan) {
        return
      }

      try {
        const detail = await getLoan(selectedLoan.loanNumber)

        if (isMounted) {
          setLoanDetail(detail)
        }
      } catch (caughtError) {
        if (isMounted) {
          setError(caughtError instanceof Error ? caughtError.message : 'Loan detail request failed')
        }
      }
    }

    void loadSelectedLoanDetail()

    return () => {
      isMounted = false
    }
  }, [selectedLoan, view])

  useEffect(() => {
    if (!loanDetail) {
      setLoanEditForm(emptyLoanEditForm())
      return
    }

    setLoanEditForm({
      type: loanDetail.type,
      stage: loanDetail.stage,
      status: loanDetail.status,
      amount: loanDetail.amount == null ? '' : String(loanDetail.amount),
      targetCloseDate: loanDetail.targetCloseDate ?? '',
      coBorrowerEmail: loanDetail.coBorrowerEmail ?? '',
      titleContactName: loanDetail.titleContactName ?? '',
      titleContactEmail: loanDetail.titleContactEmail ?? '',
      realtorName: loanDetail.realtorName ?? '',
      realtorEmail: loanDetail.realtorEmail ?? '',
      icdSent: loanDetail.icdSent,
      icdSigned: loanDetail.icdSigned,
      lastContactDate: loanDetail.lastContactDate ?? '',
    })
  }, [loanDetail])

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
    if (!customerDetail) {
      setCustomerEditForm(emptyCustomerEditForm())
      return
    }

    setCustomerEditForm({
      firstName: customerDetail.firstName,
      lastName: customerDetail.lastName,
      email: customerDetail.email ?? '',
      phone: customerDetail.phone ?? '',
      status: customerDetail.status,
    })
  }, [customerDetail])

  useEffect(() => {
    let isMounted = true

    async function loadTemplateDetail() {
      if (view !== 'admin' || !selectedTemplate) {
        setTemplateDetail(null)
        if (view === 'admin' && !selectedTemplate) {
          setTemplateForm(emptyTemplateForm())
        }
        return
      }

      try {
        const detail = await getActionTemplate(selectedTemplate.id)

        if (isMounted) {
          setTemplateDetail(detail)
          setTemplateForm(templateDetailToForm(detail))
        }
      } catch (caughtError) {
        if (isMounted) {
          setError(caughtError instanceof Error ? caughtError.message : 'Template detail request failed')
        }
      }
    }

    void loadTemplateDetail()

    return () => {
      isMounted = false
    }
  }, [selectedTemplate, view])

  useEffect(() => {
    if (!selectedAction) {
      setRescheduleDate('')
      setRescheduleReason('')
      return
    }

    setRescheduleDate(addDays(selectedAction.dueDate, 3))
    setRescheduleReason('')
    setCancelReason('')
    setReassignReason('')
    setFollowUpAction(emptyFollowUpAction())
  }, [selectedAction])

  useEffect(() => {
    const selectedLoanAction = loanDetail?.actions.find((item) => item.id === selectedAction?.id)
    setReassignUserId(selectedLoanAction?.assignedUserId ?? users.find((user) => user.isActive)?.id ?? '')
  }, [loanDetail, selectedAction, users])

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

  function cancelSelectedAction() {
    if (!selectedAction || !cancelReason.trim()) {
      return
    }

    void runWorkflow(
      () => cancelAction(selectedAction.id, cancelReason.trim()),
      `${selectedAction.id} cancelled.`,
      null,
    )
  }

  function reassignSelectedAction() {
    if (!selectedAction || !reassignUserId || !reassignReason.trim()) {
      return
    }

    void runWorkflow(
      () => reassignAction(selectedAction.id, reassignUserId, reassignReason.trim()),
      `${selectedAction.id} reassigned.`,
      selectedAction.id,
    )
  }

  function generateEmailDraftForSelectedAction() {
    if (!selectedAction) {
      return
    }

    void (async () => {
      setIsGeneratingEmailDraft(true)
      setWorkflowMessage(null)
      setError(null)

      try {
        const draft = await getActionEmailDraft(selectedAction.id)
        setEmailDraft(draft)
        setWorkflowMessage(`Email draft ready for ${selectedAction.id}.`)
      } catch (caughtError) {
        setError(caughtError instanceof Error ? caughtError.message : 'Email draft request failed')
      } finally {
        setIsGeneratingEmailDraft(false)
      }
    })()
  }

  function updateEmailDraftField(field: keyof ActionEmailDraft, value: string) {
    setEmailDraft((current) => current ? { ...current, [field]: value } : current)
  }

  function requestSendEmailDraft() {
    if (!emailDraft) {
      return
    }

    setIsEmailSendConfirmOpen(true)
  }

  function confirmSendEmailDraft() {
    if (!emailDraft) {
      setIsEmailSendConfirmOpen(false)
      return
    }

    setIsEmailSendConfirmOpen(false)
    setWorkflowMessage(`Email sent to ${emailDraft.to || 'borrower'}.`)
  }

  function discardEmailSend() {
    setIsEmailSendConfirmOpen(false)
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

  function submitLoanPageAction(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    if (!selectedLoan || !followUpAction.title.trim() || !followUpAction.dueDate) {
      return
    }

    const loanNumber = selectedLoan.loanNumber

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
        setSelectedLoanNumber(response.loanNumber)
        setLoanDetail(await getLoan(response.loanNumber))
        setFollowUpAction(emptyFollowUpAction())
        setWorkflowMessage(`Action ${response.id} added to ${loanNumber}.`)
      } catch (caughtError) {
        setError(caughtError instanceof Error ? caughtError.message : 'Create action request failed')
      } finally {
        setIsMutating(false)
      }
    })()
  }

  function updateLoanEditField(field: keyof LoanEditForm, value: string | boolean) {
    setLoanEditForm((current) => ({
      ...current,
      [field]: value,
    }))
  }

  async function submitLoanEdit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    if (!loanDetail) {
      return
    }

    setIsSavingLoan(true)
    setWorkflowMessage(null)
    setError(null)

    try {
      const saved = await updateLoan(loanDetail.loanNumber, {
        type: loanEditForm.type,
        stage: loanEditForm.stage,
        status: loanEditForm.status,
        amount: loanEditForm.amount.trim() ? Number(loanEditForm.amount) : null,
        targetCloseDate: optionalText(loanEditForm.targetCloseDate),
        coBorrowerEmail: optionalText(loanEditForm.coBorrowerEmail),
        titleContactName: optionalText(loanEditForm.titleContactName),
        titleContactEmail: optionalText(loanEditForm.titleContactEmail),
        realtorName: optionalText(loanEditForm.realtorName),
        realtorEmail: optionalText(loanEditForm.realtorEmail),
        icdSent: loanEditForm.icdSent,
        icdSigned: loanEditForm.icdSigned,
        lastContactDate: optionalText(loanEditForm.lastContactDate),
      })
      await loadWorkspace(selectedAction?.id)
      setLoanDetail(saved)
      setWorkflowMessage(`${saved.loanNumber} updated.`)
    } catch (caughtError) {
      setError(caughtError instanceof Error ? caughtError.message : 'Loan update failed')
    } finally {
      setIsSavingLoan(false)
    }
  }

  function updateCustomerEditField(field: keyof CustomerEditForm, value: string) {
    setCustomerEditForm((current) => ({
      ...current,
      [field]: value,
    }))
  }

  async function submitCustomerEdit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    if (!selectedCustomer) {
      return
    }

    setIsSavingCustomer(true)
    setWorkflowMessage(null)
    setError(null)

    try {
      const saved = await updateCustomer(selectedCustomer.id, {
        firstName: customerEditForm.firstName.trim(),
        lastName: customerEditForm.lastName.trim(),
        email: optionalText(customerEditForm.email),
        phone: optionalText(customerEditForm.phone),
        status: customerEditForm.status,
      })
      await loadWorkspace(selectedAction?.id)
      setCustomerDetail(saved)
      setSelectedCustomerId(saved.id)
      setWorkflowMessage(`${saved.borrowerName} updated.`)
    } catch (caughtError) {
      setError(caughtError instanceof Error ? caughtError.message : 'Customer update failed')
    } finally {
      setIsSavingCustomer(false)
    }
  }

  function generateTemplateActionsForSelectedLoan(templateId: string) {
    if (!selectedAction || !templateId) {
      return
    }

    const loanNumber = selectedAction.loanNumber

    void (async () => {
      setIsMutating(true)
      setWorkflowMessage(null)
      setError(null)

      try {
        const response = await generateLoanActions(loanNumber, templateId)
        const preferredActionId = response.createdActionIds[0] ?? selectedAction.id
        await loadWorkspace(preferredActionId)
        setLoanDetail(await getLoan(response.loanNumber))
        setSelectedActionId(preferredActionId)
        setWorkflowMessage(
          `${response.createdActionIds.length} action${response.createdActionIds.length === 1 ? '' : 's'} generated, ${response.skippedCount} skipped.`,
        )
      } catch (caughtError) {
        setError(caughtError instanceof Error ? caughtError.message : 'Template generation request failed')
      } finally {
        setIsMutating(false)
      }
    })()
  }

  function updateTemplateField(field: keyof Omit<TemplateFormState, 'items'>, value: string | boolean | null) {
    setTemplateForm((current) => ({
      ...current,
      [field]: value,
    }))
  }

  function updateTemplateItem(index: number, field: keyof TemplateItemForm, value: string) {
    setTemplateForm((current) => ({
      ...current,
      items: current.items.map((item, itemIndex) => (
        itemIndex === index ? { ...item, [field]: value } : item
      )),
    }))
  }

  function addTemplateItem() {
    setTemplateForm((current) => ({
      ...current,
      items: [...current.items, emptyTemplateItem(current.items.length + 1)],
    }))
  }

  function removeTemplateItem(index: number) {
    setTemplateForm((current) => ({
      ...current,
      items: current.items.length <= 1
        ? current.items
        : current.items.filter((_, itemIndex) => itemIndex !== index),
    }))
  }

  function startNewTemplate() {
    setSelectedTemplateId(null)
    setTemplateDetail(null)
    setTemplateForm(emptyTemplateForm())
  }

  async function submitTemplate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setIsSavingTemplate(true)
    setWorkflowMessage(null)
    setError(null)

    const request: UpsertActionTemplateRequest = {
      name: templateForm.name.trim(),
      loanType: templateForm.loanType,
      stage: templateForm.stage,
      isActive: templateForm.isActive,
      items: templateForm.items.map((item, index) => ({
        sortOrder: Number(item.sortOrder) || index + 1,
        section: item.section,
        title: item.title.trim(),
        description: optionalText(item.description),
        priority: item.priority,
        dueOffsetDays: Number(item.dueOffsetDays) || 0,
      })),
    }

    try {
      const saved = templateForm.id
        ? await updateActionTemplate(templateForm.id, request)
        : await createActionTemplate(request)
      const templateRows = await getActionTemplates()
      setTemplates(templateRows)
      setSelectedTemplateId(saved.id)
      setTemplateDetail(saved)
      setTemplateForm(templateDetailToForm(saved))
      setWorkflowMessage(`${saved.name} saved.`)
    } catch (caughtError) {
      setError(caughtError instanceof Error ? caughtError.message : 'Template save failed')
    } finally {
      setIsSavingTemplate(false)
    }
  }

  function updateIntakeField(field: keyof Omit<IntakeFormState, 'actions'>, value: string | boolean) {
    setIntakeForm((current) => ({
      ...current,
      [field]: value,
    }))
  }

  function updateCustomerLoanField(field: keyof Omit<IntakeFormState, 'actions'>, value: string | boolean) {
    setCustomerLoanForm((current) => ({
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

  function updateCustomerLoanAction(index: number, field: keyof IntakeActionForm, value: string) {
    setCustomerLoanForm((current) => ({
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

  function addCustomerLoanAction() {
    setCustomerLoanForm((current) => current.actions.length >= 3
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

  function removeCustomerLoanAction(index: number) {
    setCustomerLoanForm((current) => current.actions.length <= 1
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

    if (intakeForm.borrowerMode === 'existing' && !intakeForm.existingCustomerId) {
      setError('Select an existing customer before creating the loan.')
      setIsSubmittingIntake(false)
      return
    }

    try {
      const loanRequest = buildCustomerLoanRequest(intakeForm)
      const response = intakeForm.borrowerMode === 'existing'
        ? await createCustomerLoan(intakeForm.existingCustomerId, loanRequest)
        : await createFileIntake({
            customer: {
              firstName: intakeForm.firstName.trim(),
              lastName: intakeForm.lastName.trim(),
              email: optionalText(intakeForm.email),
              phone: optionalText(intakeForm.phone),
            },
            ...loanRequest,
          })

      await loadWorkspace(response.createdActionIds[0] ?? null)
      setLoanDetail(await getLoan(response.loanNumber))
      setSelectedActionId(response.createdActionIds[0] ?? null)
      setIntakeForm(emptyIntakeForm())
      setView('dashboard')
      setWorkflowMessage(
        `${response.loanNumber} created for ${response.borrowerName}${'customerMatched' in response && response.customerMatched ? ' using existing borrower.' : '.'}`,
      )
    } catch (caughtError) {
      setError(caughtError instanceof Error ? caughtError.message : 'Intake request failed')
    } finally {
      setIsSubmittingIntake(false)
    }
  }

  async function submitCustomerLoan(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    if (!selectedCustomer) {
      return
    }

    setIsSubmittingCustomerLoan(true)
    setWorkflowMessage(null)
    setError(null)

    try {
      const response = await createCustomerLoan(selectedCustomer.id, buildCustomerLoanRequest(customerLoanForm))
      const preferredActionId = response.createdActionIds[0] ?? null
      await loadWorkspace(preferredActionId)
      setLoanDetail(await getLoan(response.loanNumber))
      setSelectedActionId(preferredActionId)
      setCustomerLoanForm(emptyIntakeForm())
      setView('dashboard')
      setWorkflowMessage(`${response.loanNumber} added for ${response.borrowerName}.`)
    } catch (caughtError) {
      setError(caughtError instanceof Error ? caughtError.message : 'Customer loan request failed')
    } finally {
      setIsSubmittingCustomerLoan(false)
    }
  }

  function logOutPrototypeSession() {
    setWorkflowMessage('Logged out of prototype session. Login page is planned next.')
    setView('account')
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
          <button className={view === 'admin' ? 'active' : ''} type="button" onClick={() => setView('admin')}>
            Admin
          </button>
          <button className={view === 'account' ? 'active' : ''} type="button" onClick={() => setView('account')}>
            My Account
          </button>
        </nav>
        <div className="sidebar-footer">
          <div className="sidebar-summary">
            <span>Queue health</span>
            <strong>{dashboard.overdueCount === 0 ? 'On track' : `${dashboard.overdueCount} overdue`}</strong>
          </div>
          <button className="logout-button" type="button" onClick={logOutPrototypeSession}>
            Log out
          </button>
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
            {view === 'admin' && 'Admin templates'}
            {view === 'account' && 'My Account'}
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
                <button type="button" onClick={() => setView('intake')}>New Intake</button>
              </>
            )}
          </div>
        </header>

        {view !== 'intake' && view !== 'account' && (
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
            <article>
              <span>Closing soon</span>
              <strong>{dashboard.closingWithin7DaysCount}</strong>
              <small>Within 7 days</small>
            </article>
            <article>
              <span>ICD attention</span>
              <strong>{dashboard.icdNotSentOrSignedCount}</strong>
              <small>Not sent or unsigned</small>
            </article>
          </section>
        )}

        {workflowMessage && <p className="state-message success">{workflowMessage}</p>}

        {view === 'intake' ? (
          <IntakePage
            customers={customers.filter((customer) => customer.status === 'Active')}
            disabled={isSubmittingIntake}
            form={intakeForm}
            templates={templates.filter((template) => template.isActive)}
            onAddAction={addIntakeAction}
            onRemoveAction={removeIntakeAction}
            onSubmit={submitIntake}
            onUpdateAction={updateIntakeAction}
            onUpdateField={updateIntakeField}
          />
        ) : view === 'dashboard' ? (
          <>
          <section className="dashboard-alert-grid">
            <DashboardAlertList
              items={dashboard.closingWithin7Days}
              title="Closing within 7 days"
              onOpenLoan={(loanNumber) => {
                setSelectedLoanNumber(loanNumber)
                setView('loans')
              }}
            />
            <DashboardAlertList
              items={dashboard.icdNeedsAttention}
              title="ICD not sent/signed"
              onOpenLoan={(loanNumber) => {
                setSelectedLoanNumber(loanNumber)
                setView('loans')
              }}
            />
          </section>

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
              disabled={isMutating || isSavingLoan}
              emailDraft={emailDraft}
              followUpAction={followUpAction}
              isGeneratingEmailDraft={isGeneratingEmailDraft}
              loanEditForm={loanEditForm}
              noteDraft={noteDraft}
              templates={templates.filter((template) => template.isActive)}
              users={users.filter((user) => user.isActive)}
              onAddNote={addNote}
              onCancel={cancelSelectedAction}
              onComplete={completeSelectedAction}
              onCreateFollowUpAction={submitFollowUpAction}
              onDraftChange={setNoteDraft}
              onEmailDraftChange={updateEmailDraftField}
              onFollowUpActionChange={updateFollowUpAction}
              onGenerateEmailDraft={generateEmailDraftForSelectedAction}
              onGenerateTemplateActions={generateTemplateActionsForSelectedLoan}
              onLoanEditFieldChange={updateLoanEditField}
              onLoanEditSubmit={submitLoanEdit}
              onCancelReasonChange={setCancelReason}
              onReassign={reassignSelectedAction}
              onReassignReasonChange={setReassignReason}
              onReassignUserChange={setReassignUserId}
              onRescheduleDateChange={setRescheduleDate}
              onRescheduleReasonChange={setRescheduleReason}
              onReschedule={rescheduleSelectedAction}
              onSendEmailDraft={requestSendEmailDraft}
              cancelReason={cancelReason}
              reassignReason={reassignReason}
              reassignUserId={reassignUserId}
              rescheduleDate={rescheduleDate}
              rescheduleReason={rescheduleReason}
            />
          </section>
          </>
        ) : view === 'customers' ? (
          <section className="content-grid">
            <div className="panel customer-panel">
              <div className="panel-header">
                <div>
                  <h2>Borrower directory</h2>
                  <p>{filteredCustomers.length} visible</p>
                </div>
                <button
                  className="secondary"
                  type="button"
                  onClick={() => {
                    setIntakeForm(emptyIntakeForm())
                    setView('intake')
                  }}
                >
                  Add New Customer
                </button>
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
              addLoanForm={customerLoanForm}
              customerForm={customerEditForm}
              detail={customerDetail}
              disabled={isSavingCustomer}
              isSubmittingLoan={isSubmittingCustomerLoan}
              templates={templates.filter((template) => template.isActive)}
              onAddLoanAction={addCustomerLoanAction}
              onOpenAction={(actionId) => {
                setView('dashboard')
                setSelectedActionId(actionId)
              }}
              onOpenLoan={(loanNumber) => {
                setSelectedLoanNumber(loanNumber)
                setView('loans')
              }}
              onRemoveLoanAction={removeCustomerLoanAction}
              onSubmitLoan={submitCustomerLoan}
              onSubmit={submitCustomerEdit}
              onUpdateLoanAction={updateCustomerLoanAction}
              onUpdateLoanField={updateCustomerLoanField}
              onUpdateField={updateCustomerEditField}
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
        ) : view === 'account' ? (
          <MyAccountPage currentUser={currentUser} onLogOut={logOutPrototypeSession} />
        ) : view === 'admin' ? (
          <AdminTemplatesPage
            disabled={isSavingTemplate}
            filteredTemplates={filteredTemplates}
            form={templateForm}
            selectedTemplate={selectedTemplate}
            templateDetail={templateDetail}
            onAddItem={addTemplateItem}
            onNewTemplate={startNewTemplate}
            onRemoveItem={removeTemplateItem}
            onSelectTemplate={setSelectedTemplateId}
            onSubmit={submitTemplate}
            onUpdateField={updateTemplateField}
            onUpdateItem={updateTemplateItem}
          />
        ) : (
          <section className="content-grid">
            <div className="panel pipeline-panel">
              <div className="panel-header">
                <div>
                  <h2>Pipeline snapshot</h2>
                  <p>{filteredLoans.length} active loans</p>
                </div>
                <button
                  className="secondary"
                  type="button"
                  onClick={() => {
                    setIntakeForm(emptyIntakeForm())
                    setView('intake')
                  }}
                >
                  Add New Loan
                </button>
              </div>
              <div className="pipeline-table" role="table" aria-label="Loan pipeline">
                <div className="pipeline-heading" role="row">
                  <span>Borrower</span>
                  <span>Loan</span>
                  <span>Stage</span>
                  <span>Officer</span>
                  <span>Conditions</span>
                  <span>ICD</span>
                  <span>Next action</span>
                  <span>Due</span>
                </div>
                {filteredLoans.map((loan) => (
                  <button
                    className={`pipeline-row ${selectedLoan?.loanNumber === loan.loanNumber ? 'selected' : ''}`}
                    key={loan.loanNumber}
                    onClick={() => setSelectedLoanNumber(loan.loanNumber)}
                    role="row"
                    type="button"
                  >
                    <span>{loan.borrowerName}</span>
                    <span>{loan.loanNumber}</span>
                    <span>{loan.stage}</span>
                    <span>{loan.loanOfficerName}</span>
                    <span>{loan.totalOpenConditionCount} open</span>
                    <span>{formatIcdStatus(loan.icdSent, loan.icdSigned)}</span>
                    <span>{loan.nextActionTitle ?? 'No open action'}</span>
                    <span>{formatDueDate(loan.nextActionDueDate)} - {formatDaysToClose(loan.daysToClose)}</span>
                  </button>
                ))}
              </div>
            </div>

            <LoanPipelineDetailPanel
              actionForm={followUpAction}
              detail={loanDetail}
              disabled={isMutating}
              selected={selectedLoan}
              onActionChange={updateFollowUpAction}
              onCreateAction={submitLoanPageAction}
              onOpenAction={(actionId) => {
                setView('dashboard')
                setSelectedActionId(actionId)
              }}
            />
          </section>
        )}
      </section>
      {isEmailSendConfirmOpen && (
        <div className="modal-backdrop" role="presentation">
          <div aria-modal="true" className="confirmation-modal" role="dialog" aria-labelledby="sendEmailTitle">
            <h2 id="sendEmailTitle">Send email?</h2>
            <p>Are you sure you want to send this email?</p>
            <div className="modal-actions">
              <button type="button" onClick={confirmSendEmailDraft}>Send</button>
              <button className="secondary" type="button" onClick={discardEmailSend}>Discard</button>
            </div>
          </div>
        </div>
      )}
    </main>
  )
}

function DashboardAlertList({
  items,
  onOpenLoan,
  title,
}: {
  items?: DashboardLoanAlert[]
  onOpenLoan: (loanNumber: string) => void
  title: string
}) {
  const visibleItems = items ?? []

  return (
    <section className="panel dashboard-alert-panel">
      <div className="panel-header">
        <div>
          <h2>{title}</h2>
          <p>{visibleItems.length} loans</p>
        </div>
      </div>
      <div className="dashboard-alert-list">
        {visibleItems.slice(0, 5).map((item) => (
          <button className="dashboard-alert-row" key={`${title}-${item.loanNumber}`} type="button" onClick={() => onOpenLoan(item.loanNumber)}>
            <span>
              <strong>{item.borrowerName}</strong>
              <small>{item.loanNumber} - {formatDaysToClose(item.daysToClose)}</small>
            </span>
            <span>
              <strong>{formatDueDate(item.targetCloseDate)}</strong>
              <small>{formatIcdStatus(item.icdSent, item.icdSigned)}</small>
            </span>
          </button>
        ))}
        {visibleItems.length === 0 && <p className="state-message">No loans need attention.</p>}
      </div>
    </section>
  )
}

function LoanNeedsGroups({ actions }: { actions: LoanActionDetail[] }) {
  const sections = ['Borrower', 'Title', 'Realtor']

  return (
    <div className="needs-groups">
      {sections.map((section) => {
        const sectionActions = actions.filter((action) => action.section === section)

        return (
          <section className="needs-group" key={section}>
            <h3>{section} Needs</h3>
            {sectionActions.map((action) => (
              <div className="need-row" key={action.id}>
                <span>
                  <strong>{action.title}</strong>
                  <small>
                    {action.workflowStatus}
                    {' - '}
                    {action.priority}
                    {action.assignedUserName ? ` - ${action.assignedUserName}` : ''}
                  </small>
                </span>
                <span>
                  <strong>{formatDueDate(action.dueDate)}</strong>
                  <small>{action.completedAtUtc ? `Completed ${formatDateTime(action.completedAtUtc)}` : 'Open'}</small>
                </span>
              </div>
            ))}
            {sectionActions.length === 0 && <p>No {section.toLowerCase()} needs.</p>}
          </section>
        )
      })}
    </div>
  )
}

function LoanPipelineDetailPanel({
  actionForm,
  detail,
  disabled,
  onActionChange,
  onCreateAction,
  onOpenAction,
  selected,
}: {
  actionForm: FollowUpActionForm
  detail: LoanDetail | null
  disabled: boolean
  onActionChange: (field: keyof FollowUpActionForm, value: string) => void
  onCreateAction: (event: FormEvent<HTMLFormElement>) => void
  onOpenAction: (actionId: string) => void
  selected: LoanListItem | null
}) {
  if (!selected) {
    return (
      <aside className="panel detail-panel">
        <h2>Loan details</h2>
        <p className="state-message">Select a loan to review details and actions.</p>
      </aside>
    )
  }

  const actions = detail?.loanNumber === selected.loanNumber ? detail.actions : []
  const notes = detail?.loanNumber === selected.loanNumber ? detail.notes : []
  const history = detail?.loanNumber === selected.loanNumber ? detail.history : []

  return (
    <aside className="panel detail-panel loan-pipeline-detail">
      <div className="detail-header">
        <span className="status upcoming">{selected.status}</span>
        <h2>{selected.borrowerName}</h2>
        <p>{selected.loanNumber} - {selected.stage}</p>
      </div>

      <dl>
        <div>
          <dt>Open actions</dt>
          <dd>{selected.openActionCount}</dd>
        </div>
        <div>
          <dt>Next action</dt>
          <dd>{selected.nextActionTitle ?? 'None'}</dd>
        </div>
        <div>
          <dt>Target close</dt>
          <dd>{formatDueDate(detail?.targetCloseDate ?? null)}</dd>
        </div>
        <div>
          <dt>Borrower email</dt>
          <dd>{detail?.borrowerEmail ?? 'Not available'}</dd>
        </div>
        <div>
          <dt>Co-borrower email</dt>
          <dd>{detail?.coBorrowerEmail ?? 'Not available'}</dd>
        </div>
        <div>
          <dt>Loan officer</dt>
          <dd>{detail?.loanOfficerName ?? selected.loanOfficerName}</dd>
        </div>
        <div>
          <dt>Days to close</dt>
          <dd>{formatDaysToClose(detail?.daysToClose ?? selected.daysToClose)}</dd>
        </div>
        <div>
          <dt>ICD status</dt>
          <dd>{formatIcdStatus(detail?.icdSent ?? selected.icdSent, detail?.icdSigned ?? selected.icdSigned)}</dd>
        </div>
        <div>
          <dt>Title contact</dt>
          <dd>{detail?.titleContactName ?? 'Not available'}</dd>
        </div>
        <div>
          <dt>Title email</dt>
          <dd>{detail?.titleContactEmail ?? 'Not available'}</dd>
        </div>
        <div>
          <dt>Realtor</dt>
          <dd>{detail?.realtorName ?? 'Not available'}</dd>
        </div>
        <div>
          <dt>Realtor email</dt>
          <dd>{detail?.realtorEmail ?? 'Not available'}</dd>
        </div>
        <div>
          <dt>Last contact</dt>
          <dd>{formatDueDate(detail?.lastContactDate ?? null)}</dd>
        </div>
        <div>
          <dt>Open conditions</dt>
          <dd>
            B {detail?.borrowerOpenConditionCount ?? selected.borrowerOpenConditionCount}
            {' / '}T {detail?.titleOpenConditionCount ?? selected.titleOpenConditionCount}
            {' / '}R {detail?.realtorOpenConditionCount ?? selected.realtorOpenConditionCount}
          </dd>
        </div>
      </dl>

      <LoanNeedsGroups actions={actions} />

      <form className="create-action-box" onSubmit={onCreateAction}>
        <div className="template-items-header">
          <h3>Create action</h3>
          <button disabled={disabled || !actionForm.title.trim() || !actionForm.dueDate} type="submit">
            Create Action
          </button>
        </div>
        <div className="form-grid two-column">
          <label>
            Title
            <input
              disabled={disabled}
              onChange={(event) => onActionChange('title', event.target.value)}
              required
              value={actionForm.title}
            />
          </label>
          <label>
            Section
            <select disabled={disabled} onChange={(event) => onActionChange('section', event.target.value)} value={actionForm.section}>
              {actionSections.map((section) => <option key={section}>{section}</option>)}
            </select>
          </label>
          <label>
            Priority
            <select disabled={disabled} onChange={(event) => onActionChange('priority', event.target.value)} value={actionForm.priority}>
              {actionPriorities.map((priority) => <option key={priority}>{priority}</option>)}
            </select>
          </label>
          <label>
            Due date
            <input
              disabled={disabled}
              onChange={(event) => onActionChange('dueDate', event.target.value)}
              required
              type="date"
              value={actionForm.dueDate}
            />
          </label>
          <label className="span-all">
            Description
            <textarea
              disabled={disabled}
              onChange={(event) => onActionChange('description', event.target.value)}
              rows={3}
              value={actionForm.description}
            />
          </label>
        </div>
      </form>

      <div className="activity-feed">
        <h3>Actions</h3>
        {actions.map((action) => (
          <button className="context-row" key={action.id} type="button" onClick={() => onOpenAction(action.id)}>
            <span>
              <strong>{action.title}</strong>
              <small>{action.section} - {action.workflowStatus}</small>
            </span>
            <span>
              <strong>{formatDueDate(action.dueDate)}</strong>
              <small>{action.priority}</small>
            </span>
          </button>
        ))}
        {actions.length === 0 && <p>No actions yet.</p>}
      </div>

      <div className="activity-feed">
        <h3>Recent notes</h3>
        {notes.slice(0, 4).map((note, index) => (
          <p key={`${note.createdAtUtc}-${index}`}>
            <strong>{formatDateTime(note.createdAtUtc)}</strong>
            <span>{note.body}</span>
          </p>
        ))}
        {notes.length === 0 && <p>No notes yet.</p>}
      </div>

      <div className="activity-feed">
        <h3>History</h3>
        {history.slice(0, 4).map((event) => (
          <p key={`${event.actionId}-${event.occurredAtUtc}`}>
            <strong>{formatDateTime(event.occurredAtUtc)}</strong>
            <span>{event.actionId}: {event.eventType}</span>
          </p>
        ))}
        {history.length === 0 && <p>No history yet.</p>}
      </div>
    </aside>
  )
}

function MyAccountPage({
  currentUser,
  onLogOut,
}: {
  currentUser: CurrentUser | null
  onLogOut: () => void
}) {
  return (
    <section className="account-page">
      <div className="panel account-panel">
        <div className="panel-header">
          <div>
            <h2>User profile</h2>
            <p>{currentUser?.isActive ? 'Active session' : 'No active user loaded'}</p>
          </div>
          <button className="secondary" type="button" onClick={onLogOut}>
            Log out
          </button>
        </div>

        {currentUser ? (
          <div className="account-grid">
            <div className="readonly-field">
              <span>Display name</span>
              <strong>{currentUser.displayName}</strong>
            </div>
            <div className="readonly-field">
              <span>Email</span>
              <strong>{currentUser.email}</strong>
            </div>
            <div className="readonly-field">
              <span>Role</span>
              <strong>{currentUser.role}</strong>
            </div>
            <div className="readonly-field">
              <span>Status</span>
              <strong>{currentUser.isActive ? 'Active' : 'Inactive'}</strong>
            </div>
            <div className="readonly-field wide">
              <span>User ID</span>
              <strong>{currentUser.id}</strong>
            </div>
          </div>
        ) : (
          <p className="state-message">User profile is unavailable.</p>
        )}
      </div>
    </section>
  )
}

function AdminTemplatesPage({
  disabled,
  filteredTemplates,
  form,
  selectedTemplate,
  templateDetail,
  onAddItem,
  onNewTemplate,
  onRemoveItem,
  onSelectTemplate,
  onSubmit,
  onUpdateField,
  onUpdateItem,
}: {
  disabled: boolean
  filteredTemplates: ActionTemplateListItem[]
  form: TemplateFormState
  selectedTemplate: ActionTemplateListItem | null
  templateDetail: ActionTemplateDetail | null
  onAddItem: () => void
  onNewTemplate: () => void
  onRemoveItem: (index: number) => void
  onSelectTemplate: (id: string | null) => void
  onSubmit: (event: FormEvent<HTMLFormElement>) => void
  onUpdateField: (field: keyof Omit<TemplateFormState, 'items'>, value: string | boolean | null) => void
  onUpdateItem: (index: number, field: keyof TemplateItemForm, value: string) => void
}) {
  return (
    <section className="content-grid admin-grid">
      <div className="panel template-list-panel">
        <div className="panel-header">
          <div>
            <h2>Action templates</h2>
            <p>{filteredTemplates.length} visible</p>
          </div>
          <button className="secondary" type="button" onClick={onNewTemplate}>New template</button>
        </div>
        <div className="template-list">
          {filteredTemplates.map((template) => (
            <button
              className={`template-row ${selectedTemplate?.id === template.id ? 'selected' : ''}`}
              key={template.id}
              type="button"
              onClick={() => onSelectTemplate(template.id)}
            >
              <span>
                <strong>{template.name}</strong>
                <small>{template.loanType} - {template.stage}</small>
              </span>
              <span>
                <strong>{template.itemCount}</strong>
                <small>{template.isActive ? 'Active' : 'Inactive'}</small>
              </span>
            </button>
          ))}
          {filteredTemplates.length === 0 && <p className="state-message">No templates match this search.</p>}
        </div>
      </div>

      <form className="panel template-editor-panel" onSubmit={onSubmit}>
        <div className="panel-header">
          <div>
            <h2>{form.id ? 'Edit template' : 'New template'}</h2>
            <p>{templateDetail ? `${templateDetail.items.length} saved items` : 'Draft'}</p>
          </div>
          <button disabled={disabled} type="submit">{disabled ? 'Saving...' : 'Save template'}</button>
        </div>

        <div className="form-grid two-column">
          <label>
            Name
            <input
              disabled={disabled}
              onChange={(event) => onUpdateField('name', event.target.value)}
              required
              value={form.name}
            />
          </label>
          <label>
            Loan type
            <select disabled={disabled} onChange={(event) => onUpdateField('loanType', event.target.value)} value={form.loanType}>
              {loanTypes.map((loanType) => <option key={loanType}>{loanType}</option>)}
            </select>
          </label>
          <label>
            Stage
            <select disabled={disabled} onChange={(event) => onUpdateField('stage', event.target.value)} value={form.stage}>
              {loanStages.map((stage) => <option key={stage}>{stage}</option>)}
            </select>
          </label>
          <label className="check-label">
            <input
              checked={form.isActive}
              disabled={disabled}
              onChange={(event) => onUpdateField('isActive', event.target.checked)}
              type="checkbox"
            />
            Active
          </label>
        </div>

        <div className="template-items-header">
          <h3>Template items</h3>
          <button className="secondary" disabled={disabled || form.items.length >= 20} type="button" onClick={onAddItem}>
            Add item
          </button>
        </div>

        <div className="template-item-list">
          {form.items.map((item, index) => (
            <div className="template-item-card" key={index}>
              <div className="template-item-title">
                <strong>Item {index + 1}</strong>
                <button className="ghost" disabled={disabled || form.items.length === 1} type="button" onClick={() => onRemoveItem(index)}>
                  Remove
                </button>
              </div>
              <div className="form-grid action-grid">
                <label>
                  Title
                  <input
                    disabled={disabled}
                    onChange={(event) => onUpdateItem(index, 'title', event.target.value)}
                    required
                    value={item.title}
                  />
                </label>
                <label>
                  Section
                  <select disabled={disabled} onChange={(event) => onUpdateItem(index, 'section', event.target.value)} value={item.section}>
                    {actionSections.map((section) => <option key={section}>{section}</option>)}
                  </select>
                </label>
                <label>
                  Priority
                  <select disabled={disabled} onChange={(event) => onUpdateItem(index, 'priority', event.target.value)} value={item.priority}>
                    {actionPriorities.map((priority) => <option key={priority}>{priority}</option>)}
                  </select>
                </label>
                <label>
                  Due offset
                  <input
                    disabled={disabled}
                    onChange={(event) => onUpdateItem(index, 'dueOffsetDays', event.target.value)}
                    type="number"
                    value={item.dueOffsetDays}
                  />
                </label>
                <label>
                  Sort
                  <input
                    disabled={disabled}
                    min="1"
                    onChange={(event) => onUpdateItem(index, 'sortOrder', event.target.value)}
                    type="number"
                    value={item.sortOrder}
                  />
                </label>
                <label className="span-all">
                  Description
                  <textarea
                    disabled={disabled}
                    onChange={(event) => onUpdateItem(index, 'description', event.target.value)}
                    rows={3}
                    value={item.description}
                  />
                </label>
              </div>
            </div>
          ))}
        </div>
      </form>
    </section>
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

      <section className="panel report-panel">
        <div className="panel-header">
          <div>
            <h2>Recent activity</h2>
            <p>{summary.recentActivity.length} audit events</p>
          </div>
        </div>
        <div className="activity-list">
          {summary.recentActivity.map((activity) => (
            <div className="activity-row" key={activity.id}>
              <span>
                <strong>{activity.operation}</strong>
                <small>{activity.entityType} {activity.entityId}</small>
              </span>
              <span>
                <strong>{activity.actorName}</strong>
                <small>{formatDateTime(activity.occurredAtUtc)}</small>
              </span>
              <p>{activity.changedFields}</p>
            </div>
          ))}
          {summary.recentActivity.length === 0 && <p className="state-message">No audited activity yet.</p>}
        </div>
      </section>
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
  addLoanForm,
  customerForm,
  detail,
  disabled,
  isSubmittingLoan,
  templates,
  onAddLoanAction,
  onOpenAction,
  onOpenLoan,
  onRemoveLoanAction,
  onSubmitLoan,
  onSubmit,
  onUpdateLoanAction,
  onUpdateLoanField,
  onUpdateField,
  selected,
}: {
  addLoanForm: IntakeFormState
  customerForm: CustomerEditForm
  detail: CustomerDetail | null
  disabled: boolean
  isSubmittingLoan: boolean
  templates: ActionTemplateListItem[]
  onAddLoanAction: () => void
  onOpenAction: (actionId: string) => void
  onOpenLoan: (loanNumber: string) => void
  onRemoveLoanAction: (index: number) => void
  onSubmitLoan: (event: FormEvent<HTMLFormElement>) => void
  onSubmit: (event: FormEvent<HTMLFormElement>) => void
  onUpdateLoanAction: (index: number, field: keyof IntakeActionForm, value: string) => void
  onUpdateLoanField: (field: keyof Omit<IntakeFormState, 'actions'>, value: string | boolean) => void
  onUpdateField: (field: keyof CustomerEditForm, value: string) => void
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

  const loanDisabled = disabled || isSubmittingLoan

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

      <form className="edit-box" onSubmit={onSubmit}>
        <div>
          <h3>Profile</h3>
          <p>Borrower contact and record status.</p>
        </div>
        <div className="form-grid two-column">
          <label>
            First name
            <input
              disabled={disabled}
              onChange={(event) => onUpdateField('firstName', event.target.value)}
              required
              value={customerForm.firstName}
            />
          </label>
          <label>
            Last name
            <input
              disabled={disabled}
              onChange={(event) => onUpdateField('lastName', event.target.value)}
              required
              value={customerForm.lastName}
            />
          </label>
          <label>
            Email
            <input
              disabled={disabled}
              onChange={(event) => onUpdateField('email', event.target.value)}
              type="email"
              value={customerForm.email}
            />
          </label>
          <label>
            Phone
            <input
              disabled={disabled}
              onChange={(event) => onUpdateField('phone', event.target.value)}
              value={customerForm.phone}
            />
          </label>
          <label>
            Status
            <select disabled={disabled} onChange={(event) => onUpdateField('status', event.target.value)} value={customerForm.status}>
              {customerStatuses.map((status) => <option key={status}>{status}</option>)}
            </select>
          </label>
        </div>
        <button className="secondary" disabled={disabled} type="submit">
          {disabled ? 'Saving...' : 'Save profile'}
        </button>
      </form>

      <form className="add-loan-box" onSubmit={onSubmitLoan}>
        <div>
          <h3>Add loan</h3>
          <p>Create another file for this customer.</p>
        </div>
        <div className="form-grid two-column">
          <label>
            Loan number
            <input
              disabled={loanDisabled}
              onChange={(event) => onUpdateLoanField('loanNumber', event.target.value)}
              required
              value={addLoanForm.loanNumber}
            />
          </label>
          <label>
            Type
            <select disabled={loanDisabled} onChange={(event) => onUpdateLoanField('type', event.target.value)} value={addLoanForm.type}>
              {loanTypes.map((loanType) => <option key={loanType}>{loanType}</option>)}
            </select>
          </label>
          <label>
            Stage
            <select disabled={loanDisabled} onChange={(event) => onUpdateLoanField('stage', event.target.value)} value={addLoanForm.stage}>
              {loanStages.map((stage) => <option key={stage}>{stage}</option>)}
            </select>
          </label>
          <label>
            Amount
            <input
              disabled={loanDisabled}
              min="0"
              onChange={(event) => onUpdateLoanField('amount', event.target.value)}
              step="1000"
              type="number"
              value={addLoanForm.amount}
            />
          </label>
          <label>
            Target close
            <input
              disabled={loanDisabled}
              onChange={(event) => onUpdateLoanField('targetCloseDate', event.target.value)}
              type="date"
              value={addLoanForm.targetCloseDate}
            />
          </label>
          <label>
            Co-borrower email
            <input
              disabled={loanDisabled}
              onChange={(event) => onUpdateLoanField('coBorrowerEmail', event.target.value)}
              type="email"
              value={addLoanForm.coBorrowerEmail}
            />
          </label>
          <label>
            Title contact
            <input
              disabled={loanDisabled}
              onChange={(event) => onUpdateLoanField('titleContactName', event.target.value)}
              value={addLoanForm.titleContactName}
            />
          </label>
          <label>
            Title email
            <input
              disabled={loanDisabled}
              onChange={(event) => onUpdateLoanField('titleContactEmail', event.target.value)}
              type="email"
              value={addLoanForm.titleContactEmail}
            />
          </label>
          <label>
            Realtor name
            <input
              disabled={loanDisabled}
              onChange={(event) => onUpdateLoanField('realtorName', event.target.value)}
              value={addLoanForm.realtorName}
            />
          </label>
          <label>
            Realtor email
            <input
              disabled={loanDisabled}
              onChange={(event) => onUpdateLoanField('realtorEmail', event.target.value)}
              type="email"
              value={addLoanForm.realtorEmail}
            />
          </label>
          <label>
            Last contact
            <input
              disabled={loanDisabled}
              onChange={(event) => onUpdateLoanField('lastContactDate', event.target.value)}
              type="date"
              value={addLoanForm.lastContactDate}
            />
          </label>
          <label className="check-label">
            <input
              checked={addLoanForm.icdSent}
              disabled={loanDisabled}
              onChange={(event) => onUpdateLoanField('icdSent', event.target.checked)}
              type="checkbox"
            />
            ICD sent
          </label>
          <label className="check-label">
            <input
              checked={addLoanForm.icdSigned}
              disabled={loanDisabled}
              onChange={(event) => onUpdateLoanField('icdSigned', event.target.checked)}
              type="checkbox"
            />
            ICD signed
          </label>
          <label>
            Template
            <select disabled={loanDisabled} onChange={(event) => onUpdateLoanField('templateId', event.target.value)} value={addLoanForm.templateId}>
              <option value="">Manual actions</option>
              {templates.map((template) => (
                <option key={template.id} value={template.id}>
                  {template.name} - {template.itemCount} items
                </option>
              ))}
            </select>
          </label>
        </div>

        <div className="template-items-header">
          <h3>Initial actions</h3>
          <button className="secondary" disabled={loanDisabled || Boolean(addLoanForm.templateId) || addLoanForm.actions.length >= 3} type="button" onClick={onAddLoanAction}>
            Add action
          </button>
        </div>
        <div className="intake-actions">
          {addLoanForm.actions.map((action, index) => (
            <div className="intake-action" key={index}>
              <div className="intake-action-header">
                <strong>Action {index + 1}</strong>
                <button
                  className="ghost"
                  disabled={loanDisabled || Boolean(addLoanForm.templateId) || addLoanForm.actions.length === 1}
                  type="button"
                  onClick={() => onRemoveLoanAction(index)}
                >
                  Remove
                </button>
              </div>
              <div className="form-grid two-column">
                <label>
                  Title
                  <input
                    disabled={loanDisabled || Boolean(addLoanForm.templateId)}
                    onChange={(event) => onUpdateLoanAction(index, 'title', event.target.value)}
                    required={!addLoanForm.templateId}
                    value={action.title}
                  />
                </label>
                <label>
                  Section
                  <select disabled={loanDisabled || Boolean(addLoanForm.templateId)} onChange={(event) => onUpdateLoanAction(index, 'section', event.target.value)} value={action.section}>
                    {actionSections.map((section) => <option key={section}>{section}</option>)}
                  </select>
                </label>
                <label>
                  Priority
                  <select disabled={loanDisabled || Boolean(addLoanForm.templateId)} onChange={(event) => onUpdateLoanAction(index, 'priority', event.target.value)} value={action.priority}>
                    {actionPriorities.map((priority) => <option key={priority}>{priority}</option>)}
                  </select>
                </label>
                <label>
                  Due date
                  <input
                    disabled={loanDisabled || Boolean(addLoanForm.templateId)}
                    onChange={(event) => onUpdateLoanAction(index, 'dueDate', event.target.value)}
                    required={!addLoanForm.templateId}
                    type="date"
                    value={action.dueDate}
                  />
                </label>
                <label className="span-all">
                  Description
                  <textarea
                    disabled={loanDisabled || Boolean(addLoanForm.templateId)}
                    onChange={(event) => onUpdateLoanAction(index, 'description', event.target.value)}
                    rows={3}
                    value={action.description}
                  />
                </label>
              </div>
            </div>
          ))}
        </div>

        <label className="full-width-label">
          Initial note
          <textarea
            disabled={loanDisabled}
            onChange={(event) => onUpdateLoanField('initialNote', event.target.value)}
            rows={3}
            value={addLoanForm.initialNote}
          />
        </label>
        <button className="secondary" disabled={loanDisabled} type="submit">
          {isSubmittingLoan ? 'Adding loan...' : 'Add loan'}
        </button>
      </form>

      <div className="activity-feed">
        <h3>Loans</h3>
        {(detail?.loans.length ? detail.loans : []).map((loan) => (
          <button className="context-row" key={loan.loanNumber} type="button" onClick={() => onOpenLoan(loan.loanNumber)}>
            <span>
              <strong>{loan.loanNumber}</strong>
              <small>{loan.type} - {loan.stage} - {loan.loanOfficerName}</small>
            </span>
            <span>
              <strong>{loan.totalOpenConditionCount}</strong>
              <small>
                B {loan.borrowerOpenConditionCount}
                {' / '}T {loan.titleOpenConditionCount}
                {' / '}R {loan.realtorOpenConditionCount}
              </small>
            </span>
            <span>
              <strong>{formatIcdStatus(loan.icdSent, loan.icdSigned)}</strong>
              <small>{formatDaysToClose(loan.daysToClose)}</small>
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
  customers,
  disabled,
  form,
  templates,
  onAddAction,
  onRemoveAction,
  onSubmit,
  onUpdateAction,
  onUpdateField,
}: {
  customers: CustomerListItem[]
  disabled: boolean
  form: IntakeFormState
  templates: ActionTemplateListItem[]
  onAddAction: () => void
  onRemoveAction: (index: number) => void
  onSubmit: (event: FormEvent<HTMLFormElement>) => void
  onUpdateAction: (index: number, field: keyof IntakeActionForm, value: string) => void
  onUpdateField: (field: keyof Omit<IntakeFormState, 'actions'>, value: string | boolean) => void
}) {
  return (
    <form className="intake-page" onSubmit={onSubmit}>
      <section className="panel intake-section">
        <div className="panel-header">
          <div>
            <h2>Borrower</h2>
            <p>{form.borrowerMode === 'existing' ? 'Add this file to an existing customer' : form.email.trim() ? 'Email match will reuse an active customer' : 'New customer'}</p>
          </div>
        </div>
        <div className="segmented-control borrower-mode-control" aria-label="Borrower mode">
          <button className={form.borrowerMode === 'new' ? 'active' : ''} disabled={disabled} type="button" onClick={() => onUpdateField('borrowerMode', 'new')}>
            New borrower
          </button>
          <button className={form.borrowerMode === 'existing' ? 'active' : ''} disabled={disabled || customers.length === 0} type="button" onClick={() => onUpdateField('borrowerMode', 'existing')}>
            Existing customer
          </button>
        </div>
        {form.borrowerMode === 'existing' ? (
          <div className="form-grid two-column">
            <label>
              Customer
              <select disabled={disabled || customers.length === 0} onChange={(event) => onUpdateField('existingCustomerId', event.target.value)} required value={form.existingCustomerId}>
                {customers.map((customer) => (
                  <option key={customer.id} value={customer.id}>
                    {customer.borrowerName} - {customer.email ?? 'No email'}
                  </option>
                ))}
              </select>
            </label>
          </div>
        ) : (
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
        )}
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
          <label>
            Co-borrower email
            <input
              disabled={disabled}
              onChange={(event) => onUpdateField('coBorrowerEmail', event.target.value)}
              type="email"
              value={form.coBorrowerEmail}
            />
          </label>
          <label>
            Title contact
            <input
              disabled={disabled}
              onChange={(event) => onUpdateField('titleContactName', event.target.value)}
              value={form.titleContactName}
            />
          </label>
          <label>
            Title email
            <input
              disabled={disabled}
              onChange={(event) => onUpdateField('titleContactEmail', event.target.value)}
              type="email"
              value={form.titleContactEmail}
            />
          </label>
          <label>
            Realtor name
            <input
              disabled={disabled}
              onChange={(event) => onUpdateField('realtorName', event.target.value)}
              value={form.realtorName}
            />
          </label>
          <label>
            Realtor email
            <input
              disabled={disabled}
              onChange={(event) => onUpdateField('realtorEmail', event.target.value)}
              type="email"
              value={form.realtorEmail}
            />
          </label>
          <label>
            Last contact
            <input
              disabled={disabled}
              onChange={(event) => onUpdateField('lastContactDate', event.target.value)}
              type="date"
              value={form.lastContactDate}
            />
          </label>
          <label className="check-label">
            <input
              checked={form.icdSent}
              disabled={disabled}
              onChange={(event) => onUpdateField('icdSent', event.target.checked)}
              type="checkbox"
            />
            ICD sent
          </label>
          <label className="check-label">
            <input
              checked={form.icdSigned}
              disabled={disabled}
              onChange={(event) => onUpdateField('icdSigned', event.target.checked)}
              type="checkbox"
            />
            ICD signed
          </label>
        </div>
      </section>

      <section className="panel intake-section">
        <div className="panel-header">
          <div>
            <h2>Workflow template</h2>
            <p>{form.templateId ? 'Initial actions will be generated' : 'Manual initial actions'}</p>
          </div>
        </div>
        <div className="form-grid two-column">
          <label>
            Template
            <select disabled={disabled} onChange={(event) => onUpdateField('templateId', event.target.value)} value={form.templateId}>
              <option value="">Manual actions</option>
              {templates.map((template) => (
                <option key={template.id} value={template.id}>
                  {template.name} - {template.loanType} - {template.itemCount} items
                </option>
              ))}
            </select>
          </label>
        </div>
      </section>

      <section className="panel intake-section">
        <div className="panel-header">
          <div>
            <h2>Initial actions</h2>
            <p>{form.templateId ? 'Generated by template' : `${form.actions.length} of 3`}</p>
          </div>
          <button className="secondary" disabled={disabled || Boolean(form.templateId) || form.actions.length >= 3} type="button" onClick={onAddAction}>
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
                  disabled={disabled || Boolean(form.templateId) || form.actions.length === 1}
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
                    disabled={disabled || Boolean(form.templateId)}
                    onChange={(event) => onUpdateAction(index, 'title', event.target.value)}
                    required={!form.templateId}
                    value={action.title}
                  />
                </label>
                <label>
                  Section
                  <select disabled={disabled || Boolean(form.templateId)} onChange={(event) => onUpdateAction(index, 'section', event.target.value)} value={action.section}>
                    {actionSections.map((section) => <option key={section}>{section}</option>)}
                  </select>
                </label>
                <label>
                  Priority
                  <select disabled={disabled || Boolean(form.templateId)} onChange={(event) => onUpdateAction(index, 'priority', event.target.value)} value={action.priority}>
                    {actionPriorities.map((priority) => <option key={priority}>{priority}</option>)}
                  </select>
                </label>
                <label>
                  Due date
                  <input
                    disabled={disabled || Boolean(form.templateId)}
                    onChange={(event) => onUpdateAction(index, 'dueDate', event.target.value)}
                    required={!form.templateId}
                    type="date"
                    value={action.dueDate}
                  />
                </label>
                <label className="span-all">
                  Description
                  <textarea
                    disabled={disabled || Boolean(form.templateId)}
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
  cancelReason,
  detail,
  disabled,
  emailDraft,
  followUpAction,
  isGeneratingEmailDraft,
  loanEditForm,
  noteDraft,
  reassignReason,
  reassignUserId,
  templates,
  users,
  onAddNote,
  onCancel,
  onCancelReasonChange,
  onComplete,
  onCreateFollowUpAction,
  onDraftChange,
  onEmailDraftChange,
  onFollowUpActionChange,
  onGenerateEmailDraft,
  onGenerateTemplateActions,
  onLoanEditFieldChange,
  onLoanEditSubmit,
  onReassign,
  onReassignReasonChange,
  onReassignUserChange,
  onRescheduleDateChange,
  onRescheduleReasonChange,
  onReschedule,
  onSendEmailDraft,
  rescheduleDate,
  rescheduleReason,
}: {
  action: DashboardAction | null
  cancelReason: string
  detail: LoanDetail | null
  disabled: boolean
  emailDraft: ActionEmailDraft | null
  followUpAction: FollowUpActionForm
  isGeneratingEmailDraft: boolean
  loanEditForm: LoanEditForm
  noteDraft: string
  reassignReason: string
  reassignUserId: string
  templates: ActionTemplateListItem[]
  users: UserListItem[]
  onAddNote: () => void
  onCancel: () => void
  onCancelReasonChange: (value: string) => void
  onComplete: () => void
  onCreateFollowUpAction: () => void
  onDraftChange: (value: string) => void
  onEmailDraftChange: (field: keyof ActionEmailDraft, value: string) => void
  onFollowUpActionChange: (field: keyof FollowUpActionForm, value: string) => void
  onGenerateEmailDraft: () => void
  onGenerateTemplateActions: (templateId: string) => void
  onLoanEditFieldChange: (field: keyof LoanEditForm, value: string | boolean) => void
  onLoanEditSubmit: (event: FormEvent<HTMLFormElement>) => void
  onReassign: () => void
  onReassignReasonChange: (value: string) => void
  onReassignUserChange: (value: string) => void
  onRescheduleDateChange: (value: string) => void
  onRescheduleReasonChange: (value: string) => void
  onReschedule: () => void
  onSendEmailDraft: () => void
  rescheduleDate: string
  rescheduleReason: string
}) {
  const selectedGenerationTemplateId = templates[0]?.id ?? ''
  const selectedLoanAction = detail?.actions.find((item) => item.id === action?.id) ?? null

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

      <div className="detail-primary-actions">
        <button disabled={disabled} type="button" onClick={onComplete}>Complete</button>
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
        <div>
          <dt>Co-borrower email</dt>
          <dd>{detail?.coBorrowerEmail ?? 'Not available'}</dd>
        </div>
        <div>
          <dt>Loan officer</dt>
          <dd>{detail?.loanOfficerName ?? 'Loading'}</dd>
        </div>
        <div>
          <dt>Days to close</dt>
          <dd>{formatDaysToClose(detail?.daysToClose ?? null)}</dd>
        </div>
        <div>
          <dt>ICD status</dt>
          <dd>{detail ? formatIcdStatus(detail.icdSent, detail.icdSigned) : 'Loading'}</dd>
        </div>
        <div>
          <dt>Title contact</dt>
          <dd>{detail?.titleContactName ?? 'Not available'}</dd>
        </div>
        <div>
          <dt>Title email</dt>
          <dd>{detail?.titleContactEmail ?? 'Not available'}</dd>
        </div>
        <div>
          <dt>Realtor</dt>
          <dd>{detail?.realtorName ?? 'Not available'}</dd>
        </div>
        <div>
          <dt>Realtor email</dt>
          <dd>{detail?.realtorEmail ?? 'Not available'}</dd>
        </div>
        <div>
          <dt>Last contact</dt>
          <dd>{formatDueDate(detail?.lastContactDate ?? null)}</dd>
        </div>
        <div>
          <dt>Date added</dt>
          <dd>{detail ? formatDateTime(detail.createdAtUtc) : 'Loading'}</dd>
        </div>
        <div>
          <dt>Last updated</dt>
          <dd>{detail ? formatDateTime(detail.updatedAtUtc) : 'Loading'}</dd>
        </div>
        <div>
          <dt>Assignee</dt>
          <dd>{selectedLoanAction?.assignedUserName ?? 'Unassigned'}</dd>
        </div>
      </dl>

      <LoanNeedsGroups actions={detail?.actions ?? []} />

      <form className="edit-box" onSubmit={onLoanEditSubmit}>
        <div>
          <h3>Loan details</h3>
          <p>Stage, status, amount, and close timing.</p>
        </div>
        <div className="form-grid two-column">
          <label>
            Type
            <select disabled={disabled} onChange={(event) => onLoanEditFieldChange('type', event.target.value)} value={loanEditForm.type}>
              {loanTypes.map((loanType) => <option key={loanType}>{loanType}</option>)}
            </select>
          </label>
          <label>
            Stage
            <select disabled={disabled} onChange={(event) => onLoanEditFieldChange('stage', event.target.value)} value={loanEditForm.stage}>
              {loanStages.map((stage) => <option key={stage}>{stage}</option>)}
            </select>
          </label>
          <label>
            Status
            <select disabled={disabled} onChange={(event) => onLoanEditFieldChange('status', event.target.value)} value={loanEditForm.status}>
              {loanStatuses.map((status) => <option key={status}>{status}</option>)}
            </select>
          </label>
          <label>
            Amount
            <input
              disabled={disabled}
              min="0"
              onChange={(event) => onLoanEditFieldChange('amount', event.target.value)}
              step="1000"
              type="number"
              value={loanEditForm.amount}
            />
          </label>
          <label>
            Target close
            <input
              disabled={disabled}
              onChange={(event) => onLoanEditFieldChange('targetCloseDate', event.target.value)}
              type="date"
              value={loanEditForm.targetCloseDate}
            />
          </label>
          <label>
            Co-borrower email
            <input
              disabled={disabled}
              onChange={(event) => onLoanEditFieldChange('coBorrowerEmail', event.target.value)}
              type="email"
              value={loanEditForm.coBorrowerEmail}
            />
          </label>
          <label>
            Title contact
            <input
              disabled={disabled}
              onChange={(event) => onLoanEditFieldChange('titleContactName', event.target.value)}
              value={loanEditForm.titleContactName}
            />
          </label>
          <label>
            Title email
            <input
              disabled={disabled}
              onChange={(event) => onLoanEditFieldChange('titleContactEmail', event.target.value)}
              type="email"
              value={loanEditForm.titleContactEmail}
            />
          </label>
          <label>
            Realtor name
            <input
              disabled={disabled}
              onChange={(event) => onLoanEditFieldChange('realtorName', event.target.value)}
              value={loanEditForm.realtorName}
            />
          </label>
          <label>
            Realtor email
            <input
              disabled={disabled}
              onChange={(event) => onLoanEditFieldChange('realtorEmail', event.target.value)}
              type="email"
              value={loanEditForm.realtorEmail}
            />
          </label>
          <label>
            Last contact
            <input
              disabled={disabled}
              onChange={(event) => onLoanEditFieldChange('lastContactDate', event.target.value)}
              type="date"
              value={loanEditForm.lastContactDate}
            />
          </label>
          <label className="check-label">
            <input
              checked={loanEditForm.icdSent}
              disabled={disabled}
              onChange={(event) => onLoanEditFieldChange('icdSent', event.target.checked)}
              type="checkbox"
            />
            ICD sent
          </label>
          <label className="check-label">
            <input
              checked={loanEditForm.icdSigned}
              disabled={disabled}
              onChange={(event) => onLoanEditFieldChange('icdSigned', event.target.checked)}
              type="checkbox"
            />
            ICD signed
          </label>
        </div>
        <button className="secondary" disabled={disabled || !loanEditForm.type || !loanEditForm.stage || !loanEditForm.status} type="submit">
          {disabled ? 'Saving...' : 'Save loan'}
        </button>
      </form>

      <div className="email-draft-box">
        <div>
          <h3>Borrower email</h3>
          <p>Generate a draft for the selected condition.</p>
        </div>
        <button
          className="secondary"
          disabled={disabled || isGeneratingEmailDraft}
          type="button"
          onClick={onGenerateEmailDraft}
        >
          {isGeneratingEmailDraft ? 'Generating...' : 'Generate email'}
        </button>
        {emailDraft && (
          <div className="email-draft-preview">
            <label>
              To
              <input onChange={(event) => onEmailDraftChange('to', event.target.value)} value={emailDraft.to} />
            </label>
            <label>
              Subject
              <input onChange={(event) => onEmailDraftChange('subject', event.target.value)} value={emailDraft.subject} />
            </label>
            <label>
              Body
              <textarea onChange={(event) => onEmailDraftChange('body', event.target.value)} rows={8} value={emailDraft.body} />
            </label>
            <button
              className="secondary"
              disabled={!emailDraft.to.trim() || !emailDraft.subject.trim() || !emailDraft.body.trim()}
              type="button"
              onClick={onSendEmailDraft}
            >
              Send Email
            </button>
          </div>
        )}
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

      <div className="follow-up-box template-generate-box">
        <div>
          <h3>Generate from template</h3>
          <p>Create missing standardized actions for this loan.</p>
        </div>
        <select
          aria-label="Action template"
          disabled={disabled || templates.length === 0}
          defaultValue={selectedGenerationTemplateId}
        >
          {templates.map((template) => (
            <option key={template.id} value={template.id}>
              {template.name} - {template.itemCount} items
            </option>
          ))}
        </select>
        <button
          className="secondary"
          disabled={disabled || templates.length === 0}
          type="button"
          onClick={(event) => {
            const select = event.currentTarget.parentElement?.querySelector('select') as HTMLSelectElement | null
            onGenerateTemplateActions(select?.value ?? selectedGenerationTemplateId)
          }}
        >
          Generate actions
        </button>
      </div>

      <div className="reassign-box">
        <label htmlFor="reassignUser">Reassign</label>
        <select
          disabled={disabled || users.length === 0}
          id="reassignUser"
          onChange={(event) => onReassignUserChange(event.target.value)}
          value={reassignUserId}
        >
          {users.map((user) => (
            <option key={user.id} value={user.id}>
              {user.displayName} - {user.role}
            </option>
          ))}
        </select>
        <textarea
          aria-label="Reassignment reason"
          disabled={disabled}
          onChange={(event) => onReassignReasonChange(event.target.value)}
          placeholder="Reason for reassignment"
          rows={3}
          value={reassignReason}
        />
        <button
          className="secondary"
          disabled={disabled || !reassignUserId || !reassignReason.trim()}
          type="button"
          onClick={onReassign}
        >
          Reassign
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

      <div className="cancel-box">
        <label htmlFor="cancelReason">Cancel action</label>
        <textarea
          id="cancelReason"
          disabled={disabled}
          onChange={(event) => onCancelReasonChange(event.target.value)}
          placeholder="Reason this action no longer applies"
          rows={3}
          value={cancelReason}
        />
        <button
          className="secondary danger"
          disabled={disabled || !cancelReason.trim()}
          type="button"
          onClick={onCancel}
        >
          Cancel action
        </button>
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
