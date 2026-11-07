import { Component, ErrorInfo, ReactNode } from 'react'
import i18n from '../i18n'

type State = { failed: boolean }

export class AppErrorBoundary extends Component<{ children: ReactNode }, State> {
  state: State = { failed: false }

  static getDerivedStateFromError(): State { return { failed: true } }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('Unhandled UI error', { message: error.message, componentStack: info.componentStack })
  }

  render() {
    if (!this.state.failed) return this.props.children
    return <main className="grid min-h-screen place-items-center bg-[#f4f7f5] p-6 dark:bg-[#101713]">
      <section className="surface max-w-lg p-8 text-center" role="alert">
        <h1 className="text-2xl font-semibold">{i18n.t('errors.title')}</h1>
        <p className="muted mt-3">{i18n.t('errors.description')}</p>
        <button className="btn-primary mt-6" onClick={() => window.location.reload()}>{i18n.t('errors.reload')}</button>
      </section>
    </main>
  }
}
