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
}

export type CreateFileIntakeResponse = {
  loanNumber: string
  borrowerName: string
  customerMatched: boolean
  createdActionIds: string[]
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

export function getDashboard() {
  return getJson<DashboardSummary>('/api/v1/dashboard')
}

export function getLoans() {
  return getJson<LoanListItem[]>('/api/v1/loans')
}

export function getLoan(loanNumber: string) {
  return getJson<LoanDetail>(`/api/v1/loans/${encodeURIComponent(loanNumber)}`)
}

export function createFileIntake(request: CreateFileIntakeRequest) {
  return postJson<CreateFileIntakeResponse>('/api/v1/intake/files', request)
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
