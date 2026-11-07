import { ReactNode } from 'react'
import { X } from 'lucide-react'
import { useTranslation } from 'react-i18next'

export function Modal({ title, children, onClose, footer }: { title: string; children: ReactNode; onClose: () => void; footer?: ReactNode }) {
  return <div className="fixed inset-0 z-50 grid place-items-center bg-black/45 p-4" onMouseDown={e => e.target === e.currentTarget && onClose()}><div className="surface max-h-[92vh] w-full max-w-2xl overflow-y-auto p-6"><div className="mb-6 flex items-center justify-between"><h2 className="text-xl font-semibold">{title}</h2><button type="button" className="btn-ghost" onClick={onClose}><X/></button></div>{children}{footer && <div className="mt-7 flex justify-end gap-2">{footer}</div>}</div></div>
}
export function Field({ label, error, children, className = '' }: { label: string; error?: string; children: ReactNode; className?: string }) { return <label className={`block text-sm font-medium ${className}`}>{label}<div className="mt-1.5">{children}</div>{error && <span className="mt-1 block text-xs text-red-600">{error}</span>}</label> }
export function EmptyState({ children }: { children: ReactNode }) { return <div className="surface grid min-h-48 place-items-center p-8 text-center muted">{children}</div> }
export function PageTitle({ title, subtitle, action }: { title: string; subtitle: string; action?: ReactNode }) { return <div className="mb-7 flex flex-col justify-between gap-4 md:flex-row md:items-end"><div><h1 className="text-2xl font-semibold tracking-tight md:text-3xl">{title}</h1><p className="muted mt-1">{subtitle}</p></div>{action}</div> }
export function Status({ active }: { active: boolean }) { const { t } = useTranslation(); return <span className={`rounded-full px-2.5 py-1 text-xs font-semibold ${active ? 'bg-emerald-50 text-emerald-700 dark:bg-emerald-500/10 dark:text-emerald-300' : 'bg-slate-100 muted dark:bg-white/7'}`}>{t(active ? 'common.active' : 'common.inactive')}</span> }
