import { describe, expect, it } from 'vitest'
import { companyInitials, companyLogoHues } from './companyLogo'

describe('companyLogoHues', () => {
  // Reference values computed with the original web CompanyLogo algorithm.
  it('matches the original web colours', () => {
    expect(companyLogoHues('Northwind Freight')).toEqual({ hue: 193, hue2: 233 })
    expect(companyLogoHues('Kessler Logistik')).toEqual({ hue: 260, hue2: 300 })
    expect(companyLogoHues('')).toEqual({ hue: 61, hue2: 101 })
  })

  it('is deterministic', () => {
    expect(companyLogoHues('Acme')).toEqual(companyLogoHues('Acme'))
  })
})

describe('companyInitials', () => {
  it('takes the first letter of up to the first two words, upper-cased', () => {
    expect(companyInitials('Northwind Freight')).toBe('NF')
    expect(companyInitials('kessler logistik gmbh')).toBe('KL')
    expect(companyInitials('Solo')).toBe('S')
  })

  it('ignores extra whitespace and returns empty for a blank name', () => {
    expect(companyInitials('  Baltic   Haulage ')).toBe('BH')
    expect(companyInitials('')).toBe('')
    expect(companyInitials('   ')).toBe('')
  })
})
