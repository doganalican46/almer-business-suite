import { lazy, ReactNode, Suspense, useEffect, useState } from 'react'
import { Link, Navigate, Route, Routes } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { api, ModuleCode, Session } from './api'
import { Login } from './pages/Login'
import { ForgotPassword, ResetPassword, VerifyEmail } from './pages/AccountAccess'
import { AccountManagement } from './pages/AccountManagement'
import { Shell } from './components/Shell'

const Dashboard = lazy(() => import('./pages/Dashboard').then(m => ({ default: m.Dashboard })))
const Branches = lazy(() => import('./pages/Branches').then(m => ({ default: m.Branches })))
const Companies = lazy(() => import('./pages/Companies').then(m => ({ default: m.Companies })))
const Roles = lazy(() => import('./pages/Roles').then(m => ({ default: m.Roles })))
const Audit = lazy(() => import('./pages/Audit').then(m => ({ default: m.Audit })))
const Products = lazy(() => import('./pages/Products').then(m => ({ default: m.Products })))
const Inventory = lazy(() => import('./pages/Inventory').then(m => ({ default: m.Inventory })))
const Sales = lazy(() => import('./pages/Sales').then(m => ({ default: m.Sales })))
const Suppliers = lazy(() => import('./pages/Suppliers').then(m => ({ default: m.Suppliers })))
const Purchases = lazy(() => import('./pages/Purchases').then(m => ({ default: m.Purchases })))
const Finance = lazy(() => import('./pages/Finance').then(m => ({ default: m.Finance })))
const Campaigns = lazy(() => import('./pages/Campaigns').then(m => ({ default: m.Campaigns })))
const ProfitLoss = lazy(() => import('./pages/ProfitLoss').then(m => ({ default: m.ProfitLoss })))
const Imports = lazy(() => import('./pages/Imports').then(m => ({ default: m.Imports })))
const ReportCenter = lazy(() => import('./pages/ReportCenter').then(m => ({ default: m.ReportCenter })))
const TaxSummary = lazy(() => import('./pages/TaxSummary').then(m => ({ default: m.TaxSummary })))
const Settings = lazy(() => import('./pages/Settings').then(m => ({ default: m.Settings })))
const Security = lazy(() => import('./pages/Security').then(m => ({ default: m.Security })))
const Operations = lazy(() => import('./pages/Operations').then(m => ({ default: m.Operations })))
const ComingSoon = lazy(() => import('./pages/ComingSoon').then(m => ({ default: m.ComingSoon })))

export default function App() {
  const { t } = useTranslation()
  const [session, setSession] = useState<Session | null>(() => { try { return JSON.parse(localStorage.getItem('almer-session') ?? 'null') } catch { return null } })
  const settings = useQuery({ queryKey: ['settings'], queryFn: api.settings, enabled: !!session })
  useEffect(() => { document.documentElement.classList.toggle('dark', session?.user.theme === 'dark') }, [session])
  useEffect(()=>{const expired=()=>setSession(null);window.addEventListener('almer-session-expired',expired);return()=>window.removeEventListener('almer-session-expired',expired)},[])
  if (!session) return <Routes><Route path="/forgot-password" element={<ForgotPassword/>}/><Route path="/reset-password" element={<ResetPassword/>}/><Route path="/verify-email" element={<VerifyEmail/>}/><Route path="*" element={<><Login onLogin={setSession}/><Link to="/forgot-password" className="btn-ghost fixed bottom-5 right-5 text-xs">{t('auth.forgotPassword')}</Link></>}/></Routes>
  const gate = (code:ModuleCode, page:ReactNode) => !settings.data || settings.data.enabledModules.includes(code) ? page : <Navigate to="/dashboard" replace />
  const permitted = (codes:string[], page:ReactNode) => codes.some(code=>session.permissions.includes(code)) ? page : <Navigate to="/dashboard" replace />
  return <Suspense fallback={<div className="grid min-h-screen place-items-center text-sm muted" role="status">{t('common.loading')}</div>}><Routes><Route element={<Shell session={session} setSession={setSession} />}><Route index element={<Navigate to="/dashboard" replace />} /><Route path="/dashboard" element={gate('dashboard',<Dashboard />)} /><Route path="/branches" element={<Branches />} /><Route path="/analytics" element={gate('analytics',<ReportCenter />)} /><Route path="/sales" element={gate('sales',<Sales />)} /><Route path="/products" element={gate('products',<Products />)} /><Route path="/inventory" element={gate('inventory',<Inventory />)} /><Route path="/pricing" element={gate('pricing',<Campaigns />)} /><Route path="/purchasing" element={gate('purchasing',<Purchases />)} /><Route path="/suppliers" element={gate('suppliers',<Suppliers />)} /><Route path="/income-expenses" element={gate('finance',<Finance />)} /><Route path="/profit-loss" element={gate('reports',<ProfitLoss />)} /><Route path="/tax" element={gate('reports',<TaxSummary />)} /><Route path="/reports" element={gate('reports',<ReportCenter />)} /><Route path="/imports" element={gate('imports',<Imports />)} /><Route path="/companies" element={gate('companies',<Companies />)} /><Route path="/roles" element={gate('roles',<Roles />)} /><Route path="/settings" element={permitted(['settings.manage'],<Settings />)} /><Route path="/security" element={<Security />} /><Route path="/account" element={<AccountManagement />} /><Route path="/operations" element={permitted(['settings.manage','audit.read'],<Operations />)} /><Route path="/audit" element={gate('audit',<Audit />)} /><Route path="*" element={<ComingSoon />} /></Route></Routes></Suspense>
}
