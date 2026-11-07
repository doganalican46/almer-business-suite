/// <reference types="vite/client" />

// Recharts declares tooltip labels as ReactNode, while this app's date axis always emits ISO strings.
// The overload keeps the chart formatter type-safe without weakening the rest of the application.
import type { ReactNode } from 'react'
declare global { interface DateConstructor { new(value: ReactNode): Date } }
