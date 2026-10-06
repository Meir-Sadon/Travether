/** ISO 3166-1 alpha-2 codes; names come from Intl.DisplayNames in the UI language. */
export const countryCodes = (
  'AD AE AF AG AI AL AM AO AQ AR AS AT AU AW AX AZ BA BB BD BE BF BG BH BI BJ BL BM BN BO BQ BR BS BT BV BW BY BZ ' +
  'CA CC CD CF CG CH CI CK CL CM CN CO CR CU CV CW CX CY CZ DE DJ DK DM DO DZ EC EE EG EH ER ES ET FI FJ FK FM FO FR ' +
  'GA GB GD GE GF GG GH GI GL GM GN GP GQ GR GS GT GU GW GY HK HM HN HR HT HU ID IE IL IM IN IO IQ IR IS IT JE JM JO JP ' +
  'KE KG KH KI KM KN KP KR KW KY KZ LA LB LC LI LK LR LS LT LU LV LY MA MC MD ME MF MG MH MK ML MM MN MO MP MQ MR MS MT ' +
  'MU MV MW MX MY MZ NA NC NE NF NG NI NL NO NP NR NU NZ OM PA PE PF PG PH PK PL PM PN PR PS PT PW PY QA RE RO RS RU RW ' +
  'SA SB SC SD SE SG SH SI SJ SK SL SM SN SO SR SS ST SV SX SY SZ TC TD TF TG TH TJ TK TL TM TN TO TR TT TV TW TZ UA UG ' +
  'UM US UY UZ VA VC VE VG VI VN VU WF WS YE YT ZA ZM ZW'
).split(' ')

export function countryName(code: string, lang: string): string {
  try {
    return new Intl.DisplayNames([lang], { type: 'region' }).of(code) ?? code
  } catch {
    return code
  }
}

/** Countries sorted by their name in the UI language. */
export function countryOptions(lang: string): { code: string; name: string }[] {
  return countryCodes
    .map((code) => ({ code, name: countryName(code, lang) }))
    .sort((a, b) => a.name.localeCompare(b.name, lang))
}

/** Flag emoji from regional indicator symbols, e.g. IL → 🇮🇱. */
export function flag(code: string): string {
  return String.fromCodePoint(...[...code.toUpperCase()].map((c) => 0x1f1a5 + c.charCodeAt(0)))
}

/** Common travel languages offered as chips (ISO 639-1). */
export const languageCodes = ['en', 'es', 'fr', 'de', 'it', 'pt', 'he', 'ar', 'ru', 'zh', 'ja', 'ko', 'hi', 'th', 'nl', 'pl', 'tr', 'sv']

export function languageName(code: string, lang: string): string {
  try {
    return new Intl.DisplayNames([lang], { type: 'language' }).of(code) ?? code
  } catch {
    return code
  }
}

/** Interest tags the API accepts (ProfileRules.Interests). */
export const interestTags = [
  'hiking', 'food', 'nightlife', 'culture', 'beach', 'dayTrips', 'diving', 'budget',
  'photography', 'museums', 'music', 'sports', 'wellness', 'roadTrips', 'camping', 'shopping',
] as const
