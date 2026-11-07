import { useTranslation } from 'react-i18next'
import { Construction } from 'lucide-react'
export function ComingSoon(){const{t}=useTranslation();return <div className="grid min-h-[65vh] place-items-center"><div className="text-center"><div className="mx-auto grid size-16 place-items-center rounded-2xl bg-brand-50 text-brand-700 dark:bg-brand-500/15"><Construction size={28}/></div><h1 className="mt-5 text-xl font-semibold">{t('common.comingSoon')}</h1><p className="muted mt-2 max-w-md">Faz 3–6 kapsamında gerçek veri modeli ve iş kurallarıyla etkinleştirilecektir.</p></div></div>}

