import { describe, expect, it } from 'vitest'
import { readFileSync, readdirSync, statSync } from 'node:fs'
import { join } from 'node:path'
import de from './de-DE.json'
import tr from './tr-TR.json'
function keys(value: unknown, prefix = ''): string[] { if (!value || typeof value !== 'object') return [prefix]; return Object.entries(value).flatMap(([key, child]) => keys(child, prefix ? `${prefix}.${key}` : key)) }
function files(path:string):string[]{return readdirSync(path).flatMap(name=>{const item=join(path,name);return statSync(item).isDirectory()?files(item):item.endsWith('.tsx')||item.endsWith('.ts')?[item]:[]})}
function has(value:unknown,path:string){return path.split('.').every(part=>{if(!value||typeof value!=='object'||!(part in value))return false;value=(value as Record<string,unknown>)[part];return true})}
describe('translations', () => {
  it('German and Turkish contain the same keys', () => { expect(keys(de).sort()).toEqual(keys(tr).sort()) })
  it('every literal translation key used by the UI exists in both locales',()=>{const used=new Set<string>();for(const file of files(join(process.cwd(),'src'))){const source=readFileSync(file,'utf8');for(const match of source.matchAll(/\bt\(\s*['"]([^'"]+)['"]/g))used.add(match[1])}const missing=[...used].filter(key=>!has(de,key)||!has(tr,key));expect(missing).toEqual([])})
})

