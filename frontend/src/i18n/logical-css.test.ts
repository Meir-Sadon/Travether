import { readdirSync, readFileSync } from 'node:fs'
import { join, relative } from 'node:path'
import { describe, expect, it } from 'vitest'

/**
 * Guard for RTL (Hebrew/Arabic): styles must use logical properties so layouts mirror
 * automatically. margin-left → margin-inline-start, right: → inset-inline-end,
 * text-align: left → start, and so on. A line can opt out with a "physical-ok" comment.
 */
const srcDir = join(import.meta.dirname, '..')

const cssRules: [RegExp, string][] = [
  [/\b(margin|padding|border)-(left|right)\b/, 'use *-inline-start / *-inline-end'],
  [/(^|[\s;{])(left|right)\s*:/, 'use inset-inline-start / inset-inline-end'],
  [/\btext-align\s*:\s*(left|right)\b/, 'use text-align: start / end'],
  [/\bfloat\s*:\s*(left|right)\b/, 'use float: inline-start / inline-end'],
  [/\bborder-(top|bottom)-(left|right)-radius\b/, 'use border-start-start-radius etc.'],
]

const tsxRules: [RegExp, string][] = [
  [/\b(margin|padding|border)(Left|Right)\b/, 'use marginInlineStart etc.'],
  [/\b(left|right)\s*:\s*['"\d]/, 'use insetInlineStart / insetInlineEnd'],
  [/\btextAlign\s*:\s*['"](left|right)['"]/, "use textAlign: 'start' / 'end'"],
]

function files(dir: string, ext: string): string[] {
  return readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const path = join(dir, entry.name)
    if (entry.isDirectory()) return files(path, ext)
    return entry.name.endsWith(ext) && !entry.name.includes('.test.') ? [path] : []
  })
}

function violations(ext: string, rules: [RegExp, string][]) {
  return files(srcDir, ext).flatMap((file) =>
    readFileSync(file, 'utf8')
      .split('\n')
      .flatMap((line, i) =>
        line.includes('physical-ok')
          ? []
          : rules.filter(([re]) => re.test(line)).map(([, fix]) => `${relative(srcDir, file)}:${i + 1}: ${line.trim()} (${fix})`),
      ),
  )
}

describe('logical CSS', () => {
  it('stylesheets use logical properties', () => {
    expect(violations('.css', cssRules)).toEqual([])
  })

  it('inline styles use logical properties', () => {
    expect(violations('.tsx', tsxRules)).toEqual([])
  })
})
