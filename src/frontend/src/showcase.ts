import type { Branch, DashboardData, DashboardPreference, NotificationFeed, OperationsHealth, OperationsMetrics, ReportOverview, Session, TaxSummary, Tenant, TenantSettings } from './api'

const tenantId = '11111111-1111-1111-1111-111111111111'
const branchIds = ['21111111-1111-1111-1111-111111111111', '22111111-1111-1111-1111-111111111111', '23111111-1111-1111-1111-111111111111', '24111111-1111-1111-1111-111111111111']

const session: Session = {
  accessToken: 'showcase-only-token',
  user: { id: '31111111-1111-1111-1111-111111111111', displayName: 'Almer Demo Admin', email: 'admin@almer.demo', locale: 'de-DE', theme: 'light', emailVerified: true },
  tenant: { id: tenantId, name: 'Almer Supermarkt · Showcase', isDemo: true, currency: 'EUR' },
  permissions: ['dashboard.read', 'branches.manage', 'products.manage', 'inventory.manage', 'sales.read', 'sales.create', 'finance.read', 'finance.manage', 'reports.read', 'reports.export', 'settings.manage', 'roles.manage', 'audit.read']
}

const branches: Branch[] = [
  ['Grevenbroich', 'Grevenbroich', 'GV'], ['Bergheim', 'Bergheim', 'BH'], ['Düren', 'Düren', 'DN'], ['Alsdorf', 'Alsdorf', 'AL']
].map(([nameDe, nameTr, code], index) => ({ id: branchIds[index], legalEntityId: '41111111-1111-1111-1111-111111111111', nameDe, nameTr, code, address: 'Nordrhein-Westfalen, Deutschland', isActive: true }))

function dashboard(): DashboardData {
  const today = new Date(); const day = 86_400_000
  const trend = Array.from({ length: 30 }, (_, index) => {
    const date = new Date(today.getTime() - (29 - index) * day).toISOString().slice(0, 10)
    return { date, revenue: 31_000 + ((index * 7_913) % 18_000), expenses: 7_500 + ((index * 2_371) % 4_800) }
  })
  return {
    kpis: [
      { code: 'revenue', value: 1_248_650, change: 8.4 }, { code: 'netSales', value: 1_166_963, change: 7.9 },
      { code: 'grossProfit', value: 342_480, change: 5.6 }, { code: 'grossMargin', value: 29.3, change: 1.2 },
      { code: 'expenses', value: 194_820, change: 3.1 }, { code: 'operatingResult', value: 147_660, change: 9.7 },
      { code: 'transactions', value: 18_742, change: 6.2 }, { code: 'averageBasket', value: 66.62, change: 2.1 },
      { code: 'stockValue', value: 487_320, change: 1.8 }, { code: 'criticalStock', value: 14, change: -12.5 }
    ],
    trend,
    branches: [
      { branchId: branchIds[0], name: 'Grevenbroich', revenue: 386_400, grossProfit: 111_600 },
      { branchId: branchIds[1], name: 'Bergheim', revenue: 329_800, grossProfit: 92_700 },
      { branchId: branchIds[2], name: 'Düren', revenue: 291_250, grossProfit: 79_340 },
      { branchId: branchIds[3], name: 'Alsdorf', revenue: 241_200, grossProfit: 58_840 }
    ],
    dataQuality: { costMatchRate: 97.8, missingCostLines: 6, isComplete: false }, isDemo: true, currency: 'EUR', generatedAt: new Date().toISOString()
  }
}

const settings: TenantSettings = { primaryColor: '#147A46', enabledModules: ['dashboard', 'analytics', 'sales', 'products', 'inventory', 'pricing', 'purchasing', 'suppliers', 'finance', 'reports', 'imports', 'companies', 'roles', 'audit'], criticalStockThreshold: 10, expiryWarningDays: 14 }
const preferences: DashboardPreference = { hiddenWidgets: [], widgetOrder: [] }
const notifications: NotificationFeed = { unreadCount: 2, items: [
  { id: 'n1', type: 'CriticalStock', severity: 'Critical', title: 'Kritischer Bestand', message: '14 Artikel liegen unter dem Mindestbestand.', targetUrl: '/inventory', updatedAt: new Date().toISOString(), isRead: false },
  { id: 'n2', type: 'ExpiringStock', severity: 'Warning', title: 'Mindesthaltbarkeit', message: '8 Chargen erreichen in 14 Tagen ihr Ablaufdatum.', targetUrl: '/inventory', updatedAt: new Date().toISOString(), isRead: false }
] }

function bodyOf(init?: RequestInit) { try { return typeof init?.body === 'string' ? JSON.parse(init.body) : undefined } catch { return undefined } }

export async function showcaseRequest<T>(path: string, init?: RequestInit): Promise<T> {
  const method = init?.method ?? 'GET'
  let value: unknown
  if (path === '/auth/login') value = session
  else if (path.startsWith('/dashboard/preferences')) value = method === 'PUT' ? bodyOf(init) : preferences
  else if (path.startsWith('/dashboard')) value = dashboard()
  else if (path === '/branches') value = branches
  else if (path === '/settings') value = method === 'PUT' ? bodyOf(init) : settings
  else if (path.startsWith('/notifications')) value = path.includes('/read') || method === 'PUT' ? undefined : notifications
  else if (path.startsWith('/search')) value = []
  else if (path === '/tenant') value = { id: tenantId, name: session.tenant.name, slug: 'almer-showcase', defaultLocale: 'de-DE', currency: 'EUR', isDemo: true, isActive: true } satisfies Tenant
  else if (path === '/legal-entities') value = [{ id: '41111111-1111-1111-1111-111111111111', nameDe: 'Almer Feinkost GmbH', nameTr: 'Almer Feinkost GmbH', tradeName: 'Almer Supermarkt', taxNumber: 'DEMO', countryCode: 'DE', isActive: true }]
  else if (path.startsWith('/products')) value = path.includes('/prices') ? [] : { items: [], total: 0, page: 1, pageSize: 50 }
  else if (path.startsWith('/profitability')) value = { netSales: 1_166_963, costOfGoodsSold: 824_483, grossProfit: 342_480, grossMargin: 29.3, operatingExpenses: 194_820, otherIncome: 0, operatingResult: 147_660, dataQuality: { costMatchRate: 97.8, missingCostLines: 6, isComplete: false }, byProduct: [] }
  else if (path.startsWith('/reports/overview')) value = { from: '', to: '', summary: { netSales: 1_166_963, costOfGoodsSold: 824_483, grossProfit: 342_480, grossMargin: 29.3, operatingExpenses: 194_820, otherIncome: 0, operatingResult: 147_660, costMatchRate: 97.8, missingCostLines: 6, isComplete: false }, daily: [], byBranch: [], byCategory: [], expenses: [], topProducts: [], lowMarginProducts: [], generatedAt: new Date().toISOString() } satisfies ReportOverview
  else if (path.startsWith('/reports/tax-summary')) value = { from: '', to: '', rates: [], outputTax: 0, inputTax: 0, netTaxPayable: 0, isOfficialFiling: false, disclaimer: 'Showcase data – keine Steuererklärung.' } satisfies TaxSummary
  else if (path === '/operations/health') value = { status: 'healthy', queue: { queued: 0, running: 0, failedLast24Hours: 0, stale: 0 }, checkedAt: new Date().toISOString() } satisfies OperationsHealth
  else if (path === '/operations/metrics') value = { runtime: { requests: 1284, serverErrors: 0, averageDurationMs: 42.7, statusCodes: { 200: 1268, 204: 16 }, completedJobs: 24, failedJobs: 0, processId: 0, workingSetBytes: 0, capturedAt: new Date().toISOString() }, tenant: { jobs: [], activeSessions: 1, activeNotifications: 2 } } satisfies OperationsMetrics
  else if (method !== 'GET') value = bodyOf(init)
  else value = []
  await new Promise(resolve => setTimeout(resolve, 120))
  return value as T
}

export function showcaseLogout() { localStorage.removeItem('almer-token'); localStorage.removeItem('almer-session') }
