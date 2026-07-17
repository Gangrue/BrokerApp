export type DashboardAction = {
  id: string
  borrowerName: string
  loanNumber: string
  title: string
  section: string
  bucket: string
  priority: string
  dueDate: string
}

export type DashboardSummary = {
  overdueCount: number
  dueTodayCount: number
  upcomingCount: number
  openActions: DashboardAction[]
}

export type LoanListItem = {
  loanNumber: string
  borrowerName: string
  stage: string
  status: string
  priority: string
  openActionCount: number
  nextActionTitle: string | null
  nextActionDueDate: string | null
}

export type LoanDetail = {
  loanNumber: string
  borrowerName: string
  borrowerEmail: string | null
  borrowerPhone: string | null
  type: string
  stage: string
  status: string
  amount: number | null
  targetCloseDate: string | null
  actions: LoanActionDetail[]
  notes: LoanNote[]
  history: ActionEvent[]
}

export type LoanActionDetail = {
  id: string
  title: string
  section: string
  workflowStatus: string
  priority: string
  dueDate: string
  completedAtUtc: string | null
  assignedUserId: string | null
  assignedUserName: string | null
}

export type LoanNote = {
  body: string
  createdAtUtc: string
}

export type ActionEvent = {
  actionId: string
  eventType: string
  reason: string | null
  oldValue: string | null
  newValue: string | null
  occurredAtUtc: string
}

export type CustomerListItem = {
  id: string
  borrowerName: string
  email: string | null
  phone: string | null
  status: string
  loanCount: number
  openActionCount: number
  nextActionTitle: string | null
  nextActionDueDate: string | null
}

export type CustomerDetail = {
  id: string
  firstName: string
  lastName: string
  borrowerName: string
  email: string | null
  phone: string | null
  status: string
  loans: CustomerLoan[]
  openActions: CustomerAction[]
}

export type CustomerLoan = {
  loanNumber: string
  type: string
  stage: string
  status: string
  targetCloseDate: string | null
  openActionCount: number
  nextActionTitle: string | null
  nextActionDueDate: string | null
}

export type CustomerAction = {
  id: string
  loanNumber: string
  title: string
  section: string
  priority: string
  dueDate: string
}

export type ReportSummary = {
  metrics: ReportMetric[]
  pipelineByStage: ReportBreakdown[]
  openActionsBySection: ReportBreakdown[]
  openActionsByPriority: ReportBreakdown[]
  upcomingClosings: ReportUpcomingClosing[]
  oldestOpenActions: ReportAgingAction[]
  recentActivity: ReportActivity[]
}

export type ReportMetric = {
  label: string
  value: number
}

export type ReportBreakdown = {
  label: string
  value: number
}

export type ReportUpcomingClosing = {
  loanNumber: string
  borrowerName: string
  stage: string
  targetCloseDate: string | null
  openActionCount: number
}

export type ReportAgingAction = {
  id: string
  borrowerName: string
  loanNumber: string
  title: string
  section: string
  priority: string
  dueDate: string
  daysOpen: number
}

export type ReportActivity = {
  id: string
  entityType: string
  entityId: string
  operation: string
  changedFields: string
  actorName: string
  occurredAtUtc: string
}

export type CreateFileIntakeRequest = {
  customer: {
    firstName: string
    lastName: string
    email: string | null
    phone: string | null
  }
  loan: {
    loanNumber: string
    type: string
    stage: string
    amount: number | null
    targetCloseDate: string | null
  }
  actions: Array<{
    title: string
    section: string
    priority: string
    dueDate: string
    description: string | null
  }>
  initialNote: string | null
  templateId?: string | null
}

export type CreateFileIntakeResponse = {
  loanNumber: string
  borrowerName: string
  customerMatched: boolean
  createdActionIds: string[]
}

export type CreateLoanActionRequest = {
  title: string
  section: string
  priority: string
  dueDate: string
  description: string | null
}

export type CreateLoanActionResponse = {
  id: string
  loanNumber: string
  borrowerName: string
  title: string
  section: string
  priority: string
  dueDate: string
}

export type UpdateCustomerRequest = {
  firstName: string
  lastName: string
  email: string | null
  phone: string | null
  status: string
}

export type UpdateLoanRequest = {
  type: string
  stage: string
  status: string
  amount: number | null
  targetCloseDate: string | null
}

export type ActionTemplateListItem = {
  id: string
  name: string
  loanType: string
  stage: string
  isActive: boolean
  itemCount: number
}

export type UserListItem = {
  id: string
  displayName: string
  email: string
  role: string
  isActive: boolean
}

export type ActionTemplateDetail = {
  id: string
  name: string
  loanType: string
  stage: string
  isActive: boolean
  items: ActionTemplateItem[]
}

export type ActionTemplateItem = {
  id: string
  sortOrder: number
  section: string
  title: string
  description: string | null
  priority: string
  dueOffsetDays: number
}

export type UpsertActionTemplateRequest = {
  name: string
  loanType: string
  stage: string
  isActive: boolean
  items: Array<{
    sortOrder: number
    section: string
    title: string
    description: string | null
    priority: string
    dueOffsetDays: number
  }>
}

export type GenerateLoanActionsResponse = {
  loanNumber: string
  templateId: string
  createdActionIds: string[]
  skippedCount: number
}

async function readErrorMessage(response: Response) {
  try {
    const body = await response.json() as { message?: string }

    return body.message ?? `Request failed with ${response.status}`
  } catch {
    return `Request failed with ${response.status}`
  }
}

async function getJson<T>(url: string): Promise<T> {
  const response = await fetch(url)

  if (!response.ok) {
    throw new Error(await readErrorMessage(response))
  }

  return await response.json() as T
}

async function postJson<T>(url: string, body: unknown): Promise<T> {
  const response = await fetch(url, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(body),
  })

  if (!response.ok) {
    throw new Error(await readErrorMessage(response))
  }

  return await response.json() as T
}

async function putJson<T>(url: string, body: unknown): Promise<T> {
  const response = await fetch(url, {
    method: 'PUT',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(body),
  })

  if (!response.ok) {
    throw new Error(await readErrorMessage(response))
  }

  return await response.json() as T
}

export function getDashboard() {
  return getJson<DashboardSummary>('/api/v1/dashboard')
}

export function getLoans() {
  return getJson<LoanListItem[]>('/api/v1/loans')
}

export function getLoan(loanNumber: string) {
  return getJson<LoanDetail>(`/api/v1/loans/${encodeURIComponent(loanNumber)}`)
}

export function getCustomers() {
  return getJson<CustomerListItem[]>('/api/v1/customers')
}

export function getCustomer(id: string) {
  return getJson<CustomerDetail>(`/api/v1/customers/${encodeURIComponent(id)}`)
}

export function updateCustomer(id: string, request: UpdateCustomerRequest) {
  return putJson<CustomerDetail>(`/api/v1/customers/${encodeURIComponent(id)}`, request)
}

export function getReportSummary() {
  return getJson<ReportSummary>('/api/v1/reports/summary')
}

export function getActionTemplates() {
  return getJson<ActionTemplateListItem[]>('/api/v1/action-templates')
}

export function getUsers() {
  return getJson<UserListItem[]>('/api/v1/users')
}

export function getActionTemplate(id: string) {
  return getJson<ActionTemplateDetail>(`/api/v1/action-templates/${encodeURIComponent(id)}`)
}

export function updateLoan(loanNumber: string, request: UpdateLoanRequest) {
  return putJson<LoanDetail>(`/api/v1/loans/${encodeURIComponent(loanNumber)}`, request)
}

export function createActionTemplate(request: UpsertActionTemplateRequest) {
  return postJson<ActionTemplateDetail>('/api/v1/action-templates', request)
}

export function updateActionTemplate(id: string, request: UpsertActionTemplateRequest) {
  return putJson<ActionTemplateDetail>(`/api/v1/action-templates/${encodeURIComponent(id)}`, request)
}

export function createFileIntake(request: CreateFileIntakeRequest) {
  return postJson<CreateFileIntakeResponse>('/api/v1/intake/files', request)
}

export function createLoanAction(loanNumber: string, request: CreateLoanActionRequest) {
  return postJson<CreateLoanActionResponse>(`/api/v1/loans/${encodeURIComponent(loanNumber)}/actions`, request)
}

export function generateLoanActions(loanNumber: string, templateId: string) {
  return postJson<GenerateLoanActionsResponse>(`/api/v1/loans/${encodeURIComponent(loanNumber)}/generate-actions`, {
    templateId,
  })
}

export function completeAction(publicId: string) {
  return postJson(`/api/v1/actions/${encodeURIComponent(publicId)}/complete`, {
    reason: 'Completed from workflow prototype.',
  })
}

export function rescheduleAction(publicId: string, dueDate: string, reason: string) {
  return postJson(`/api/v1/actions/${encodeURIComponent(publicId)}/reschedule`, {
    dueDate,
    reason,
  })
}

export function addActionComment(publicId: string, body: string) {
  return postJson(`/api/v1/actions/${encodeURIComponent(publicId)}/comments`, {
    body,
  })
}

export function cancelAction(publicId: string, reason: string) {
  return postJson(`/api/v1/actions/${encodeURIComponent(publicId)}/cancel`, {
    reason,
  })
}

export function reassignAction(publicId: string, assignedUserId: string, reason: string) {
  return postJson(`/api/v1/actions/${encodeURIComponent(publicId)}/reassign`, {
    assignedUserId,
    reason,
  })
}
