export type Session = { accessToken: string; user: { id: string; displayName: string; email: string; locale: string; theme: 'light' | 'dark'; emailVerified:boolean }; tenant: { id: string; name: string; isDemo: boolean; currency: string }; permissions: string[] }
export type MfaStatus = { enabled: boolean; setupPending: boolean; recoveryCodesRemaining: number }
export type MfaSetup = { secret: string; otpauthUri: string }
export type UserSession = { id:string; userAgent?:string; ipAddress?:string; createdAt:string; lastUsedAt:string; expiresAt:string; isCurrent:boolean }
export type NotificationChannelSettings = { emailEnabled:boolean; emailRecipients:string[]; webhookEnabled:boolean; webhookUrl?:string; hasWebhookSecret:boolean }
export type AvailableTenant = { id:string;name:string;slug:string;currency:string;isDemo:boolean;isActive:boolean }
export type OperationsHealth={status:string;queue:{queued:number;running:number;failedLast24Hours:number;stale:number};checkedAt:string}
export type OperationsMetrics={runtime:{requests:number;serverErrors:number;averageDurationMs:number;statusCodes:Record<string,number>;completedJobs:number;failedJobs:number;processId:number;workingSetBytes:number;capturedAt:string};tenant:{jobs:{status:string;count:number}[];activeSessions:number;activeNotifications:number}}
export type DashboardData = { kpis: { code: string; value: number; change?: number }[]; trend: { date: string; revenue: number; expenses: number }[]; branches: { branchId: string; name: string; revenue: number; grossProfit: number }[]; dataQuality: { costMatchRate: number; missingCostLines: number; isComplete: boolean }; isDemo: boolean; currency: string; generatedAt: string }
export type Branch = { id: string; legalEntityId: string; nameDe: string; nameTr: string; code: string; address: string; managerName?: string; phone?: string; openedOn?: string; isActive: boolean }
export type LegalEntity = { id: string; nameDe: string; nameTr: string; tradeName: string; taxNumber: string; countryCode: string; isActive: boolean }
export type Role = { id: string; nameDe: string; nameTr: string; isSystem: boolean; permissions: string[] }
export type Tenant = { id: string; name: string; slug: string; defaultLocale: string; currency: string; isDemo: boolean; isActive: boolean }
export type User = { id: string; displayName: string; email: string; roleId: string; branchIds: string[]; isActive: boolean; locale: string }
export type AuditEntry = { id: string; userId?: string; action: string; entityType: string; entityId: string; occurredAt: string }
export type Category = { id: string; parentId?: string; nameDe: string; nameTr: string; sortOrder: number; isActive: boolean }
export type Unit = { id: string; nameDe: string; nameTr: string; symbol: string; baseUnitId?: string; conversionFactor: number; isActive: boolean }
export type Product = { id: string; sku: string; barcode?: string; nameDe: string; nameTr: string; descriptionDe: string; descriptionTr: string; brand?: string; categoryId: string; unitId: string; taxClassCode: string; purchaseCost: number; salesPrice: number; minimumStock: number; imageUrl?: string; isActive: boolean }
export type StockBalance = { branchId: string; branchNameDe: string; branchNameTr: string; productId: string; sku: string; productNameDe: string; productNameTr: string; quantity: number; minimumStock: number; isCritical: boolean }
export type StockMovementType = 'Opening'|'Purchase'|'Sale'|'CustomerReturn'|'SupplierReturn'|'Waste'|'CountAdjustment'|'TransferOut'|'TransferIn'
export type StockMovement = { id: string; branchId: string; productId: string; type: StockMovementType; quantityDelta: number; unitCost?: number; reference?: string; note?: string; lotNumber?: string; expiresOn?: string; transferGroupId?: string; occurredAt: string }
export type ProductPrice = { id: string; productId: string; branchId?: string; price: number; validFrom: string; validTo?: string }
export type Supplier = { id: string; code: string; name: string; taxNumber?: string; email?: string; phone?: string; address?: string; isActive: boolean }
export type TaxRule = { id: string; code: string; countryCode: string; name: { de: string; tr: string }; rate: number; validFrom: string; validTo?: string; isActive: boolean }
export type SaleLine = { id: string; productId: string; productNameSnapshot: string; quantity: number; unitPrice: number; unitCost: number; discountAmount: number; taxRate: number; netAmount: number; taxAmount: number; grossAmount: number }
export type Sale = { id: string; branchId: string; transactionNumber: string; occurredAt: string; paymentMethod: string; sourceSystem: string; isReturn: boolean; netAmount: number; taxAmount: number; grossAmount: number; discountAmount: number; lines: SaleLine[] }
export type Purchase = { id: string; branchId: string; supplierId: string; documentNumber: string; date: string; netAmount: number; taxAmount: number; grossAmount: number; lines: { id:string; productId:string; productNameSnapshot:string; quantity:number; unitCost:number; taxRate:number; netAmount:number; taxAmount:number; grossAmount:number }[] }
export type FinancialEntryType = 'Income'|'Expense'
export type FinancialCategory = { id: string; type: FinancialEntryType; name: { de: string; tr: string }; isActive: boolean }
export type FinancialEntry = { id: string; branchId?: string; categoryId: string; type: FinancialEntryType; date: string; netAmount: number; taxRate: number; taxAmount: number; grossAmount: number; description: string; paymentMethod: string; isRecurring: boolean; isApproved: boolean }
export type Campaign = { id:string; name:{de:string;tr:string}; type:'Percentage'|'FixedAmount'; value:number; productId?:string; categoryId?:string; branchId?:string; startsAt:string; endsAt:string; priority:number; isActive:boolean }
export type Profitability = { netSales:number; costOfGoodsSold:number; grossProfit:number; grossMargin:number; operatingExpenses:number; otherIncome:number; operatingResult:number; dataQuality:{costMatchRate:number;missingCostLines:number;isComplete:boolean}; byProduct:{productId:string;name:string;netSales:number;cost:number;grossProfit:number}[] }

const base = import.meta.env.VITE_API_URL ?? '/api'
const showcaseMode = import.meta.env.VITE_SHOWCASE_MODE === 'true'
let token = localStorage.getItem('almer-token')
let refreshPromise:Promise<boolean>|null=null
function storeSession(session:Session){token=session.accessToken;localStorage.setItem('almer-token',token);localStorage.setItem('almer-session',JSON.stringify(session))}
function clearSession(){token=null;localStorage.removeItem('almer-token');localStorage.removeItem('almer-session')}
async function refreshAccess(){try{let response=await fetch(`${base}/auth/refresh`,{method:'POST',credentials:'include'});if(response.status===409){await new Promise(resolve=>setTimeout(resolve,300));response=await fetch(`${base}/auth/refresh`,{method:'POST',credentials:'include'})}if(!response.ok){clearSession();window.dispatchEvent(new Event('almer-session-expired'));return false}storeSession(await response.json());return true}catch{clearSession();window.dispatchEvent(new Event('almer-session-expired'));return false}}
async function request<T>(path: string, init?: RequestInit, retry=true): Promise<T> {
  if (showcaseMode) return showcaseRequest<T>(path, init)
  const headers = new Headers(init?.headers)
  if (!(init?.body instanceof FormData)) headers.set('Content-Type', 'application/json')
  if (token) headers.set('Authorization', `Bearer ${token}`)
  const response = await fetch(`${base}${path}`, { ...init, headers, credentials:'include' })
  if(response.status===401&&retry&&token&&!path.startsWith('/auth/')){refreshPromise??=refreshAccess().finally(()=>{refreshPromise=null});if(await refreshPromise)return request<T>(path,init,false)}
  if (!response.ok) throw new Error(`${response.status}`)
  return response.status === 204 ? undefined as T : response.json()
}
export const api = {
  login: async (email: string, password: string, mfaCode?: string) => { try { const session = await request<Session>('/auth/login', { method: 'POST', body: JSON.stringify({ email, password, mfaCode }) }); storeSession(session); return session } catch(error) { if(error instanceof Error&&error.message==='428')throw new Error('MFA_REQUIRED',{cause:error}); throw error } },
  logout: async () => { if(showcaseMode){showcaseLogout();clearSession();return} try{await fetch(`${base}/auth/logout`,{method:'POST',credentials:'include'})}finally{clearSession()} },
  dashboard: (branchId?: string, from?: string, to?: string) => { const query = new URLSearchParams(); if (branchId) query.set('branchId', branchId); if (from) query.set('from', from); if (to) query.set('to', to); return request<DashboardData>(`/dashboard?${query}`) },
  branches: () => request<Branch[]>('/branches'),
  createBranch: (value: Omit<Branch, 'id' | 'isActive'>) => request<Branch>('/branches', { method: 'POST', body: JSON.stringify(value) }),
  updateBranch: (id: string, value: Omit<Branch, 'id'>) => request<Branch>(`/branches/${id}`, { method: 'PUT', body: JSON.stringify(value) }),
  deactivateBranch: (id: string) => request<void>(`/branches/${id}`, { method: 'DELETE' }),
  legalEntities: () => request<LegalEntity[]>('/legal-entities'),
  createLegalEntity: (value: Omit<LegalEntity, 'id' | 'isActive'>) => request<LegalEntity>('/legal-entities', { method: 'POST', body: JSON.stringify(value) }),
  updateLegalEntity: (id: string, value: Omit<LegalEntity, 'id'>) => request<LegalEntity>(`/legal-entities/${id}`, { method: 'PUT', body: JSON.stringify(value) }),
  deactivateLegalEntity: (id: string) => request<void>(`/legal-entities/${id}`, { method: 'DELETE' }),
  roles: () => request<Role[]>('/roles'),
  permissions: () => request<string[]>('/permissions'),
  createRole: (value: Pick<Role, 'nameDe' | 'nameTr' | 'permissions'>) => request<Role>('/roles', { method: 'POST', body: JSON.stringify(value) }),
  updateRole: (id: string, value: Pick<Role, 'nameDe' | 'nameTr' | 'permissions'>) => request<Role>(`/roles/${id}`, { method: 'PUT', body: JSON.stringify(value) }),
  deleteRole: (id: string) => request<void>(`/roles/${id}`, { method: 'DELETE' }),
  tenant: () => request<Tenant>('/tenant'),
  updateTenant: (value: Pick<Tenant, 'name' | 'defaultLocale' | 'currency'>) => request<Tenant>('/tenant', { method: 'PUT', body: JSON.stringify(value) }),
  users: () => request<User[]>('/users'),
  createUser: (value: Omit<User, 'id' | 'isActive'> & { password: string }) => request<User>('/users', { method: 'POST', body: JSON.stringify(value) }),
  updateUser: (id: string, value: Omit<User, 'id'>) => request<User>(`/users/${id}`, { method: 'PUT', body: JSON.stringify(value) }),
  audit: () => request<AuditEntry[]>('/audit'),
  categories: () => request<Category[]>('/categories'),
  createCategory: (value: Omit<Category, 'id'|'isActive'>) => request<Category>('/categories', { method: 'POST', body: JSON.stringify(value) }),
  updateCategory: (id: string, value: Omit<Category, 'id'>) => request<Category>(`/categories/${id}`, { method: 'PUT', body: JSON.stringify(value) }),
  units: () => request<Unit[]>('/units'),
  createUnit: (value: Omit<Unit, 'id'|'isActive'>) => request<Unit>('/units', { method: 'POST', body: JSON.stringify(value) }),
  updateUnit: (id: string, value: Omit<Unit, 'id'>) => request<Unit>(`/units/${id}`, { method: 'PUT', body: JSON.stringify(value) }),
  products: (search = '', categoryId = '', page = 1) => { const query = new URLSearchParams({ page: String(page), pageSize: '50' }); if (search) query.set('search', search); if (categoryId) query.set('categoryId', categoryId); return request<{ items: Product[]; total: number; page: number; pageSize: number }>(`/products?${query}`) },
  createProduct: (value: Omit<Product, 'id'|'isActive'>) => request<Product>('/products', { method: 'POST', body: JSON.stringify(value) }),
  updateProduct: (id: string, value: Omit<Product, 'id'>) => request<Product>(`/products/${id}`, { method: 'PUT', body: JSON.stringify(value) }),
  productPrices: (id: string) => request<ProductPrice[]>(`/products/${id}/prices`),
  setBranchPrice: (id: string, branchId: string, price: number) => request<ProductPrice>(`/products/${id}/prices`, { method: 'POST', body: JSON.stringify({ branchId, price }) }),
  inventoryBalances: (branchId = '', search = '') => { const query = new URLSearchParams(); if (branchId) query.set('branchId', branchId); if (search) query.set('search', search); return request<StockBalance[]>(`/inventory/balances?${query}`) },
  inventoryMovements: (branchId = '', productId = '') => { const query = new URLSearchParams(); if (branchId) query.set('branchId', branchId); if (productId) query.set('productId', productId); return request<StockMovement[]>(`/inventory/movements?${query}`) },
  createMovement: (value: { branchId: string; productId: string; type: StockMovementType; quantity: number; unitCost?: number; reference?: string; note?: string; lotNumber?: string; expiresOn?: string }) => request<StockMovement>('/inventory/movements', { method: 'POST', body: JSON.stringify(value) }),
  transferStock: (value: { fromBranchId: string; toBranchId: string; productId: string; quantity: number; reference?: string; note?: string }) => request<{ transferGroupId: string }>('/inventory/transfers', { method: 'POST', body: JSON.stringify(value) }),
  taxRules: () => request<TaxRule[]>('/tax-rules'),
  createTaxRule: (value: {code:string;countryCode:string;nameDe:string;nameTr:string;rate:number;validFrom:string;validTo?:string}) => request<TaxRule>('/tax-rules',{method:'POST',body:JSON.stringify(value)}),
  suppliers: () => request<Supplier[]>('/suppliers'),
  createSupplier: (value: Omit<Supplier,'id'|'isActive'>) => request<Supplier>('/suppliers',{method:'POST',body:JSON.stringify(value)}),
  updateSupplier: (id:string,value:Omit<Supplier,'id'>) => request<Supplier>(`/suppliers/${id}`,{method:'PUT',body:JSON.stringify(value)}),
  purchases: () => request<Purchase[]>('/purchases'),
  createPurchase: (value:{branchId:string;supplierId:string;documentNumber:string;date:string;lines:{productId:string;quantity:number;unitCost:number}[]}) => request<Purchase>('/purchases',{method:'POST',body:JSON.stringify(value)}),
  sales: () => request<Sale[]>('/sales'),
  createSale: (value:{branchId:string;transactionNumber:string;occurredAt:string;paymentMethod:string;sourceSystem:string;isReturn:boolean;lines:{productId:string;quantity:number;unitPrice?:number;discountAmount?:number}[]}) => request<Sale>('/sales',{method:'POST',body:JSON.stringify(value)}),
  financialCategories: () => request<FinancialCategory[]>('/financial-categories'),
  createFinancialCategory: (value:{type:FinancialEntryType;nameDe:string;nameTr:string}) => request<FinancialCategory>('/financial-categories',{method:'POST',body:JSON.stringify(value)}),
  financialEntries: () => request<FinancialEntry[]>('/financial-entries'),
  createFinancialEntry: (value:{branchId?:string;categoryId:string;type:FinancialEntryType;date:string;netAmount:number;taxRate:number;description:string;paymentMethod:string;isRecurring:boolean;documentUrl?:string}) => request<FinancialEntry>('/financial-entries',{method:'POST',body:JSON.stringify(value)}),
  approveFinancialEntry: (id:string,isApproved:boolean) => request<FinancialEntry>(`/financial-entries/${id}/approval`,{method:'PUT',body:JSON.stringify({isApproved})}),
  campaigns: () => request<Campaign[]>('/campaigns'),
  createCampaign: (value:{nameDe:string;nameTr:string;type:'Percentage'|'FixedAmount';value:number;productId?:string;categoryId?:string;branchId?:string;startsAt:string;endsAt:string;priority:number}) => request<Campaign>('/campaigns',{method:'POST',body:JSON.stringify(value)}),
  updateCampaign: (id:string,value:{nameDe:string;nameTr:string;type:'Percentage'|'FixedAmount';value:number;productId?:string;categoryId?:string;branchId?:string;startsAt:string;endsAt:string;priority:number;isActive:boolean}) => request<Campaign>(`/campaigns/${id}`,{method:'PUT',body:JSON.stringify(value)}),
  profitability: (from:string,to:string,branchId='') => {const q=new URLSearchParams({from,to});if(branchId)q.set('branchId',branchId);return request<Profitability>(`/profitability?${q}`)},
  imports: () => request<ImportBatch[]>('/imports'),
  uploadImport: (file:File,type:ImportType,options:ImportOptions,branchId?:string) => {const body=new FormData();body.append('file',file);const q=new URLSearchParams({type,delimiter:options.delimiter,encoding:options.encoding,decimalSeparator:options.decimalSeparator,dateFormat:options.dateFormat});if(branchId)q.set('branchId',branchId);return request<ImportUpload>(`/imports/upload?${q}`,{method:'POST',body})},
  validateImport: (id:string,value:{mapping:Record<string,string>;options:ImportOptions}) => request<ImportValidation>(`/imports/${id}/validate`,{method:'PUT',body:JSON.stringify(value)}),
  commitImport: (id:string) => request<{jobId:string;status:'Queued'}>(`/imports/${id}/commit`,{method:'POST'}),
  retryBackgroundJob: (id:string) => request<{id:string;status:'Queued'}>(`/background-jobs/${id}/retry`,{method:'POST'}),
  downloadImportTemplate: async (type:ImportType) => {const response=await fetch(`${base}/imports/template/${type}`,{headers:token?{Authorization:`Bearer ${token}`}:{}});if(!response.ok)throw new Error(String(response.status));const blob=await response.blob();const url=URL.createObjectURL(blob);const a=document.createElement('a');a.href=url;a.download=`almer-${type.toLowerCase()}-template.xlsx`;a.click();URL.revokeObjectURL(url)},
  importTemplates: (type?:ImportType) => request<ImportMappingTemplate[]>(`/import-mapping-templates${type?`?type=${type}`:''}`),
  saveImportTemplate: (value:{name:string;type:ImportType;mapping:Record<string,string>;options:ImportOptions}) => request<ImportMappingTemplate>('/import-mapping-templates',{method:'POST',body:JSON.stringify(value)}),
  reportOverview: (from:string,to:string,branchId='',locale='de-DE') => {const q=new URLSearchParams({from,to,locale});if(branchId)q.set('branchId',branchId);return request<ReportOverview>(`/reports/overview?${q}`)},
  taxSummary: (from:string,to:string,branchId='') => {const q=new URLSearchParams({from,to});if(branchId)q.set('branchId',branchId);return request<TaxSummary>(`/reports/tax-summary?${q}`)},
  downloadReport: async (report:ReportType,format:ReportFormat,from:string,to:string,branchId='',locale='de-DE') => {const q=new URLSearchParams({report,format,from,to,locale});if(branchId)q.set('branchId',branchId);const response=await fetch(`${base}/reports/export?${q}`,{headers:token?{Authorization:`Bearer ${token}`}:{}});if(!response.ok)throw new Error(String(response.status));const blob=await response.blob();const disposition=response.headers.get('content-disposition')??'';const fileName=disposition.match(/filename\*?=(?:UTF-8''|")?([^";]+)/i)?.[1]??`almer-${report}.${format}`;const url=URL.createObjectURL(blob);const a=document.createElement('a');a.href=url;a.download=decodeURIComponent(fileName);a.click();URL.revokeObjectURL(url)},
  settings: () => request<TenantSettings>('/settings'),
  updateSettings: (value:TenantSettings) => request<TenantSettings>('/settings',{method:'PUT',body:JSON.stringify(value)}),
  dashboardPreferences: () => request<DashboardPreference>('/dashboard/preferences'),
  updateDashboardPreferences: (value:DashboardPreference) => request<DashboardPreference>('/dashboard/preferences',{method:'PUT',body:JSON.stringify(value)}),
  search: (q:string) => request<SearchResult[]>(`/search?q=${encodeURIComponent(q)}`),
  notifications: (locale:string) => request<NotificationFeed>(`/notifications?locale=${encodeURIComponent(locale)}`),
  markNotificationRead: (id:string) => request<void>(`/notifications/${id}/read`,{method:'PUT'}),
  markAllNotificationsRead: () => request<void>('/notifications/read-all',{method:'PUT'}),
  mfaStatus: () => request<MfaStatus>('/mfa/status'),
  mfaSetup: () => request<MfaSetup>('/mfa/setup',{method:'POST'}),
  mfaEnable: (code:string) => request<{recoveryCodes:string[]}>('/mfa/enable',{method:'POST',body:JSON.stringify({code})}),
  mfaDisable: (password:string,code:string) => request<void>('/mfa/disable',{method:'POST',body:JSON.stringify({password,code})}),
  mfaRecoveryCodes: (password:string,code:string) => request<{recoveryCodes:string[]}>('/mfa/recovery-codes',{method:'POST',body:JSON.stringify({password,code})}),
  sessions: () => request<UserSession[]>('/sessions'),
  revokeSession: (id:string) => request<void>(`/sessions/${id}`,{method:'DELETE'}),
  notificationChannels: () => request<NotificationChannelSettings>('/notification-channels'),
  updateNotificationChannels: (value:{emailEnabled:boolean;emailRecipients:string[];webhookEnabled:boolean;webhookUrl?:string;webhookSecret?:string}) => request<NotificationChannelSettings>('/notification-channels',{method:'PUT',body:JSON.stringify(value)}),
  forgotPassword: (email:string) => request<void>('/auth/password/forgot',{method:'POST',body:JSON.stringify({email})}),
  resetPassword: (tokenValue:string,newPassword:string) => request<void>('/auth/password/reset',{method:'POST',body:JSON.stringify({token:tokenValue,newPassword})}),
  verifyEmail: (tokenValue:string) => request<void>('/auth/email/verify',{method:'POST',body:JSON.stringify({token:tokenValue})}),
  changePassword: (currentPassword:string,newPassword:string) => request<void>('/users/me/password',{method:'POST',body:JSON.stringify({currentPassword,newPassword})}),
  sendEmailVerification: () => request<void>('/users/me/email-verification',{method:'POST'}),
  availableTenants: () => request<AvailableTenant[]>('/tenants/available'),
  switchTenant: async (tenantId:string) => {const session=await request<Session>('/auth/switch-tenant',{method:'POST',body:JSON.stringify({tenantId})});storeSession(session);return session},
  operationsHealth:()=>request<OperationsHealth>('/operations/health'),
  operationsMetrics:()=>request<OperationsMetrics>('/operations/metrics'),
  preferences: (locale: string, theme: string) => request<void>('/users/me/preferences', { method: 'PUT', body: JSON.stringify({ locale, theme }) })
}

export type ImportType='Product'|'Sale'|'Stock'|'Expense'|'Supplier'|'Purchase'
export type ImportStatus='Uploaded'|'Validated'|'Queued'|'Processing'|'Completed'|'Failed'
export type ImportOptions={delimiter:string;encoding:string;decimalSeparator:string;dateFormat:string}
export type ImportBatch={id:string;branchId?:string;type:ImportType;status:ImportStatus;fileName:string;totalRows:number;validRows:number;errorRows:number;importedRows:number;createdAt:string;completedAt?:string;backgroundJobId?:string;failureMessage?:string}
export type ImportUpload={id:string;type:ImportType;fileName:string;columns:string[];fields:string[];suggestedMapping:Record<string,string>;preview:Record<string,string>[];totalRows:number}
export type ImportValidation={totalRows:number;validRows:number;errors:{row:number;field:string;message:string}[];preview:Record<string,string>[]}
export type ImportMappingTemplate={id:string;name:string;type:ImportType;mappingJson:string;optionsJson:string;createdAt:string}
export type ReportType='sales'|'purchases'|'expenses'|'inventory'|'profitability'|'tax'
export type ReportFormat='csv'|'xlsx'|'pdf'
export type ReportOverview={from:string;to:string;summary:{netSales:number;costOfGoodsSold:number;grossProfit:number;grossMargin:number;operatingExpenses:number;otherIncome:number;operatingResult:number;costMatchRate:number;missingCostLines:number;isComplete:boolean};daily:{date:string;netSales:number;grossSales:number;transactions:number}[];byBranch:{branchId:string;name:string;netSales:number;grossProfit:number;transactions:number}[];byCategory:{categoryId?:string;name:string;netSales:number;grossProfit:number}[];expenses:{categoryId:string;name:string;amount:number}[];topProducts:{productId:string;name:string;netSales:number;grossProfit:number;quantity:number;margin:number}[];lowMarginProducts:{productId:string;name:string;netSales:number;grossProfit:number;quantity:number;margin:number}[];generatedAt:string}
export type TaxSummary={from:string;to:string;rates:{rate:number;salesNet:number;outputTax:number;purchaseInputTax:number;expenseInputTax:number;netTax:number}[];outputTax:number;inputTax:number;netTaxPayable:number;isOfficialFiling:boolean;disclaimer:string}
export type ModuleCode='dashboard'|'analytics'|'sales'|'products'|'inventory'|'pricing'|'purchasing'|'suppliers'|'finance'|'reports'|'imports'|'companies'|'roles'|'audit'
export type TenantSettings={logoUrl?:string;primaryColor:string;enabledModules:ModuleCode[];criticalStockThreshold:number;expiryWarningDays:number}
export type DashboardPreference={hiddenWidgets:string[];widgetOrder:string[]}
export type SearchResult={type:'product'|'branch'|'supplier'|'sale';title:string;subtitle:string;url:string}
export type NotificationFeed={unreadCount:number;items:{id:string;type:'CriticalStock'|'ExpiringStock'|'System';severity:'Info'|'Warning'|'Critical';title:string;message:string;targetUrl?:string;branchId?:string;productId?:string;updatedAt:string;isRead:boolean}[]}

import { showcaseLogout, showcaseRequest } from './showcase'

