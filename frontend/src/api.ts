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
  closingWithin7DaysCount: number
  icdNotSentOrSignedCount: number
  closingWithin7Days: DashboardLoanAlert[]
  icdNeedsAttention: DashboardLoanAlert[]
  openActions: DashboardAction[]
}

export type DashboardLoanAlert = {
  loanNumber: string
  borrowerName: string
  targetCloseDate: string | null
  daysToClose: number | null
  loanOfficerName: string
  icdSent: boolean
  icdSigned: boolean
}

export type LoanListItem = {
  loanNumber: string
  borrowerName: string
  stage: string
  status: string
  priority: string
  openActionCount: number
  borrowerOpenConditionCount: number
  titleOpenConditionCount: number
  realtorOpenConditionCount: number
  totalOpenConditionCount: number
  nextActionTitle: string | null
  nextActionDueDate: string | null
  targetCloseDate: string | null
  daysToClose: number | null
  loanOfficerName: string
  icdSent: boolean
  icdSigned: boolean
}

export type LoanDetail = {
  loanNumber: string
  borrowerName: string
  borrowerEmail: string | null
  borrowerPhone: string | null
  coBorrowerEmail: string | null
  type: string
  stage: string
  status: string
  amount: number | null
  targetCloseDate: string | null
  daysToClose: number | null
  loanOfficerName: string
  titleContactName: string | null
  titleContactEmail: string | null
  realtorName: string | null
  realtorEmail: string | null
  icdSent: boolean
  icdSigned: boolean
  lastContactDate: string | null
  createdAtUtc: string
  updatedAtUtc: string
  borrowerOpenConditionCount: number
  titleOpenConditionCount: number
  realtorOpenConditionCount: number
  totalOpenConditionCount: number
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

export type ActionEmailDraft = {
  to: string
  subject: string
  body: string
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
  daysToClose: number | null
  loanOfficerName: string
  icdSent: boolean
  icdSigned: boolean
  borrowerOpenConditionCount: number
  titleOpenConditionCount: number
  realtorOpenConditionCount: number
  totalOpenConditionCount: number
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
    coBorrowerEmail: string | null
    titleContactName: string | null
    titleContactEmail: string | null
    realtorName: string | null
    realtorEmail: string | null
    icdSent: boolean
    icdSigned: boolean
    lastContactDate: string | null
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

export type CreateCustomerLoanRequest = {
  loan: {
    loanNumber: string
    type: string
    stage: string
    amount: number | null
    targetCloseDate: string | null
    coBorrowerEmail: string | null
    titleContactName: string | null
    titleContactEmail: string | null
    realtorName: string | null
    realtorEmail: string | null
    icdSent: boolean
    icdSigned: boolean
    lastContactDate: string | null
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

export type CreateCustomerLoanResponse = {
  loanNumber: string
  borrowerName: string
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
  coBorrowerEmail: string | null
  titleContactName: string | null
  titleContactEmail: string | null
  realtorName: string | null
  realtorEmail: string | null
  icdSent: boolean
  icdSigned: boolean
  lastContactDate: string | null
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
  emailConfirmed: boolean
}

export type CurrentUser = {
  id: string
  organizationId: string
  organizationName: string
  displayName: string
  email: string
  role: string
  isActive: boolean
  emailConfirmed: boolean
}

export type AuthResult = {
  user: CurrentUser | null
  requiresEmailConfirmation: boolean
  debugLink: string | null
}

export type RegisterRequest = {
  organizationName: string
  displayName: string
  email: string
  password: string
}

export type LoginRequest = {
  email: string
  password: string
  rememberMe: boolean
}

export type ForgotPasswordResponse = {
  message: string
  debugLink: string | null
}

export type CreateUserRequest = {
  displayName: string
  email: string
  role: string
}

export type CreateUserResponse = {
  user: UserListItem
  confirmationDebugLink: string | null
  passwordResetDebugLink: string | null
}

export type ResendUserInvitationResponse = {
  user: UserListItem
  confirmationDebugLink: string | null
  passwordResetDebugLink: string | null
}

export type UpdateUserStatusRequest = {
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

const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '')
let csrfToken: string | null = null

export class AuthRequiredError extends Error {
  constructor() {
    super('Authentication is required.')
  }
}

function apiUrl(path: string) {
  return `${apiBaseUrl}${path}`
}

async function ensureCsrfToken() {
  if (csrfToken) {
    return csrfToken
  }

  return await refreshCsrfToken()
}

async function refreshCsrfToken() {
  clearCsrfToken()

  const response = await fetch(apiUrl('/api/v1/auth/csrf'), {
    cache: 'no-store',
    credentials: 'include',
  })

  if (!response.ok) {
    throw new Error(await readErrorMessage(response))
  }

  const body = await response.json() as { csrfToken: string }
  csrfToken = body.csrfToken

  if (!csrfToken) {
    throw new Error('CSRF token was not issued.')
  }

  return csrfToken
}

function clearCsrfToken() {
  csrfToken = null
}

async function readErrorMessage(response: Response) {
  try {
    const body = await response.json() as {
      detail?: string
      errors?: Record<string, string[]>
      message?: string
      title?: string
    }

    if (body.message) {
      return body.message
    }

    if (body.errors) {
      const messages = Object.values(body.errors).flat()

      if (messages.length > 0) {
        return messages.join(' ')
      }
    }

    return body.detail ?? body.title ?? `Request failed with ${response.status}`
  } catch {
    return `Request failed with ${response.status}`
  }
}

async function shouldRetryWithFreshCsrf(response: Response) {
  if (response.status !== 400) {
    return false
  }

  try {
    const body = await response.json() as {
      detail?: string
      errors?: Record<string, string[]>
      message?: string
      title?: string
    }

    return body.title === 'Bad Request'
      && !body.message
      && !body.detail
      && !body.errors
  } catch {
    return false
  }
}

async function getJson<T>(url: string): Promise<T> {
  const response = await fetch(apiUrl(url), {
    credentials: 'include',
  })

  if (response.status === 401) {
    throw new AuthRequiredError()
  }

  if (!response.ok) {
    throw new Error(await readErrorMessage(response))
  }

  if (response.status === 204) {
    return undefined as T
  }

  const text = await response.text()

  return text ? JSON.parse(text) as T : undefined as T
}

async function postJson<T>(url: string, body: unknown): Promise<T> {
  return sendJson<T>('POST', url, body)
}

async function putJson<T>(url: string, body: unknown): Promise<T> {
  return sendJson<T>('PUT', url, body)
}

async function deleteJson<T>(url: string): Promise<T> {
  return sendJson<T>('DELETE', url)
}

async function sendJson<T>(method: 'POST' | 'PUT' | 'DELETE', url: string, body?: unknown): Promise<T> {
  const token = await ensureCsrfToken()
  let response = await fetch(apiUrl(url), {
    method,
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
      'X-XSRF-TOKEN': token,
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  })

  if (await shouldRetryWithFreshCsrf(response.clone())) {
    clearCsrfToken()
    const retryToken = await ensureCsrfToken()
    response = await fetch(apiUrl(url), {
      method,
      credentials: 'include',
      headers: {
        'Content-Type': 'application/json',
        'X-XSRF-TOKEN': retryToken,
      },
      body: body === undefined ? undefined : JSON.stringify(body),
    })
  }

  if (response.status === 401) {
    throw new AuthRequiredError()
  }

  if (!response.ok) {
    throw new Error(await readErrorMessage(response))
  }

  if (response.status === 204) {
    return undefined as T
  }

  const text = await response.text()

  return text ? JSON.parse(text) as T : undefined as T
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

export function createUser(request: CreateUserRequest) {
  return postJson<CreateUserResponse>('/api/v1/users', request)
}

export function updateUserStatus(id: string, request: UpdateUserStatusRequest) {
  return putJson<UserListItem>(`/api/v1/users/${encodeURIComponent(id)}/status`, request)
}

export function resendUserInvitation(id: string) {
  return postJson<ResendUserInvitationResponse>(`/api/v1/users/${encodeURIComponent(id)}/resend-invitation`, {})
}

export function getCurrentUser() {
  return getJson<CurrentUser>('/api/v1/auth/me')
}

export async function register(request: RegisterRequest) {
  const result = await postJson<AuthResult>('/api/v1/auth/register', request)
  await refreshCsrfToken()

  return result
}

export async function login(request: LoginRequest) {
  const result = await postJson<AuthResult>('/api/v1/auth/login', request)
  await refreshCsrfToken()

  return result
}

export async function logout() {
  await postJson('/api/v1/auth/logout', {})
  await refreshCsrfToken()
}

export function forgotPassword(email: string) {
  return postJson<ForgotPasswordResponse>('/api/v1/auth/forgot-password', { email })
}

export function resetPassword(email: string, token: string, newPassword: string) {
  return postJson<{ message: string }>('/api/v1/auth/reset-password', {
    email,
    token,
    newPassword,
  })
}

export function confirmEmail(email: string, token: string) {
  return getJson<{ message: string }>(`/api/v1/auth/confirm-email?email=${encodeURIComponent(email)}&token=${encodeURIComponent(token)}`)
}

export function getActionTemplate(id: string) {
  return getJson<ActionTemplateDetail>(`/api/v1/action-templates/${encodeURIComponent(id)}`)
}

export function getActionEmailDraft(publicId: string) {
  return getJson<ActionEmailDraft>(`/api/v1/actions/${encodeURIComponent(publicId)}/email-draft`)
}

export function updateLoan(loanNumber: string, request: UpdateLoanRequest) {
  return putJson<LoanDetail>(`/api/v1/loans/${encodeURIComponent(loanNumber)}`, request)
}

export function deleteLoan(loanNumber: string) {
  return deleteJson<void>(`/api/v1/loans/${encodeURIComponent(loanNumber)}`)
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

export function createCustomerLoan(customerId: string, request: CreateCustomerLoanRequest) {
  return postJson<CreateCustomerLoanResponse>(`/api/v1/customers/${encodeURIComponent(customerId)}/loans`, request)
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
