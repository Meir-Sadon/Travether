# Trust & Safety, Identity Verification, Privacy & Legal: Evaluation of the Travether Spec

Scope: evaluate the founder's spec (national-ID registration and login, contact-detail reveal after manager approval, public profiles with age and rating, ratings written when an activity is deleted as "done together", proximity sorting by the activity's origin location) against current best practice, as of October 2026.

Research note: many primary pages (Pearl Cohen, law.co.il, TechCrunch, Couchsurfing support) were blocked by the egress proxy, so several findings rely on search-result summaries of those pages. Treat exact figures as "reported by" and check them before quoting externally.

## Q1. Is a national ID number (teudat zehut) a good identifier and login for a global app? What do comparable apps use?

### Takeaway
No. Most of the target audience has no Israeli ID. National ID numbers are flagged as high-risk identifiers under both Israeli and EU law. Regulators explicitly tell businesses to minimize collecting them. No comparable app (Tinder, Bumble, Airbnb, Couchsurfing) uses a national ID number as a login. Use email or phone plus OAuth (Google/Apple) and passkeys for login. If verification is needed, check an ID document (passport, driver's licence or any national ID) through a vendor that does not keep the number in Travether's database.

### Cited Findings
- Israel's Privacy Protection Authority (PPA) says an ID number counts as "information" under the Privacy Protection Law because it is a unique identifier that can be used to reach further personal information. It says businesses should follow data minimization, prefer less invasive ways to verify identity, and ask for ID card photos only in rare cases of clear necessity (front side only). — [Pearl Cohen summary of PPA guidance](https://www.pearlcohen.com/israeli-privacy-regulator-says-businesses-should-minimize-their-processing-of-national-ids/); [law.co.il: "Businesses should minimize their processing of national IDs" (June 2023)](https://law.co.il/en/news/2023/06/22/businesses-should-minimize-their-processing-of-national-ids); [Pearl Cohen: PPA guidelines on national ID cards](https://www.pearlcohen.com/israeli-regulator-issues-guidelines-on-national-id-cards-and-digital-health-data/)
- Since Amendment 13 (14 Aug 2025), the Israeli definition of an "identifiable person" explicitly lists "identification number" alongside biometric identifier, location data and online identifier. — [Baker McKenzie Israel key definitions](https://resourcehub.bakermckenzie.com/en/resources/global-data-and-cyber-handbook/emea/israel/topics/key-definitions)
- GDPR Art. 87 lets EU Member States set specific, often stricter, conditions for processing a "national identification number or any other identifier of general application", with appropriate safeguards. Commentary notes these identifiers raise the risk of cross-system linkage, identity theft and profiling. — [GDPR Art. 87 text](https://gdpr-text.com/es/read/article-87/); [Presencis Art. 87 commentary](https://presencis.com/regulations/gdpr/article-87/)
- The Israeli ID is a 9-digit number with a check digit. Off-the-shelf DLP tools such as Microsoft Purview and Palo Alto treat it as a sensitive data type to detect and protect. — [Microsoft Purview: Israel national ID SIT](https://learn.microsoft.com/id-id/purview/sit-defn-israel-national-identification-number); [Palo Alto DLP: National ID – Israel](https://docs.paloaltonetworks.com/content/techdocs/en_US/enterprise-dlp/reference/predefined-data-pattern-keywords/nationalid-israel.html)
- Comparable platforms verify against a photo of any government ID plus a selfie, and they don't use the ID as a login. Couchsurfing accepts passports, driver's licences and national ID cards, checked by photo and a video selfie in the app. Airbnb accepts national ID, driving licence, passport, state, tribal ID or residence permit plus a selfie, and says the ID and selfie are never shown to other users. — [Couchsurfing: accepted ID types](https://support.couchsurfing.org/hc/en-us/articles/50048521645211-What-Types-of-ID-Are-Accepted-for-Verification); [Couchsurfing: What is verification](https://support.couchsurfing.org/hc/en-us/articles/49997779463067-What-Is-Verification-and-How-Do-I-Get-Verified); [Airbnb Help 3033](https://www.airbnb.co.uk/help/article/3033)
- Passkeys: Yahoo! JAPAN reports 28M active passkey users, with about 50% of its smartphone authentications on passkeys. Reports say passkeys are 2.6x faster than SMS OTP and cut login help-desk incidents by 81%. — [FIDO Alliance, World Passkey Day 2025](https://fidoalliance.org/celebrating-world-passkey-day-2025-showcase-of-real-world-passkey-deployments/); [Authsignal (vendor blog)](https://www.authsignal.com/blog/articles/passwordless-authentication-in-2025-the-year-passkeys-went-mainstream)
- SMS OTP cost and fraud: a US SMS OTP costs about $0.013 at list price and an India-routed one about $0.083. SMS pumping (artificially inflated traffic) reportedly cost Twitter about $60M a year. — [MojoAuth (vendor blog)](https://mojoauth.com/blog/how-to-cut-your-ott-sms-otp-bill-with-passkeys-in-2026); [Authsignal](https://www.authsignal.com/blog/articles/passwordless-authentication-in-2025-the-year-passkeys-went-mainstream). Both are vendor sources, so treat the figures as indicative.

### Inferences
- **Change:** drop the national ID number as a registration field and as the login. It excludes most travelers and creates a high-value breach target. It also conflicts with the PPA's minimization guidance and invites stricter national rules under GDPR Art. 87. A sequential or guessable ID number also makes credential stuffing easier.
- **Recommended login:** email (magic link or password) or phone OTP, plus "Sign in with Google" and "Sign in with Apple". Note that Apple's App Store rules generally require Sign in with Apple if other third-party social logins are offered in an iOS app. This is from background knowledge; check the current App Review Guideline 4.8. Add passkeys as the main login once the MVP is stable. Add rate limits, CAPTCHA and geo-permissions on SMS to limit SMS pumping.
- **Recommended verification:** an optional or tiered "Verified" badge using a vendor flow (document plus selfie liveness). Store only the vendor's verification ID, the result, the date, and possibly the verified name and date of birth. Don't store the document number or images.
- "Country of origin" from a list is low risk. Make it optional or show it only as a flag, and make sure it isn't used for discrimination or filtering abuse.

### Gaps
- I could not open the PPA's original Hebrew guidance (fetches were blocked). Check the exact wording at gov.il/PPA before quoting.
- No source I found addresses using an ID number as a username or password specifically. The conclusion above is inferred from the minimization guidance.

## Q2. Legal: GDPR, Israel PPL Amendment 13, CCPA, age, consent, retention, deletion, location, DSA

### Takeaway
Travether will likely fall under Israeli law (founder and controller in Israel), GDPR (it offers services to people in the EU, Art. 3(2)), and possibly CCPA/CPRA once it passes the size thresholds. The practical baseline is GDPR-grade: a lawful basis per purpose, data minimization, an 18+ age gate, deletion and export on request, defined retention, and coarse location by default. Amendment 13 adds real fines and DPO obligations in Israel. The DSA adds notice-and-action and statement-of-reasons duties even for small platforms, with some exemptions for micro and small enterprises.

### Cited Findings
- **Amendment 13:** in force since 14 Aug 2025 (approved 5 Aug 2024). It is the largest overhaul of Israel's 1981 Privacy Protection Law and moves definitions closer to GDPR. — [Vixio](https://www.vixio.com/insights/pc-regulatory-influencer-israel-modernises-privacy-law-comprehensive); [Pearl Cohen](https://www.pearlcohen.com/the-knesset-enacts-a-comprehensive-amendment-to-the-privacy-protection-law/)
- Amendment 13 replaces "sensitive data" with "data of special sensitivity". This covers health, biometric and genetic data, sexual orientation, criminal history, political opinions, financial information and other prescribed categories. — [Vixio](https://www.vixio.com/insights/pc-regulatory-influencer-israel-modernises-privacy-law-comprehensive); [Baker McKenzie](https://resourcehub.bakermckenzie.com/en/resources/global-data-and-cyber-handbook/emea/israel/topics/key-definitions). Location data is explicitly an identifier. Search summaries did not say whether location counts as "special sensitivity"; see Gaps.
- Under Amendment 13, database registration is required only for databases with more than 10,000 data subjects or run by public bodies. A DPO is required for entities whose core activity involves "extended and systematic monitoring". The PPA said it would enforce the DPO duty from 31 Oct 2025. Administrative fines reach up to NIS 320,000 per violation (NIS 640,000 in aggravated cases), as reported. — [Clym](https://www.clym.io/blog/amendment-13-israels-updated-privacy-protection-law-and-what-businesses-must-do-now); [Pearl Cohen](https://www.pearlcohen.com/major-amendment-to-israeli-privacy-law-set-to-take-effect/); [Mondaq](https://www.mondaq.com/privacy-protection/1709950/major-amendment-to-privacy-law-in-israel). These figures come from secondary summaries and differ in detail.
- **GDPR:** Art. 87 covers national identification numbers (see Q1). — [GDPR Art. 87](https://gdpr-text.com/es/read/article-87/). Other relevant articles: Art. 5 (minimization and storage limitation), Art. 6 (lawful basis), Art. 8 (children's consent for information-society services, age 13–16 depending on the Member State), Art. 9 (biometric data used to uniquely identify a person is special-category data, which matters for selfie liveness), Art. 17 (erasure), Art. 20 (portability), Art. 35 (DPIA for high-risk processing). — [GDPR text, gdpr-info.eu](https://gdpr-info.eu/)
- **DSA:** every hosting provider needs a notice-and-action mechanism for illegal content, statements of reasons for removals, and a way for users to contest decisions. Art. 19 exempts micro and small enterprises (under 50 staff and under EUR 10M turnover) from some online-platform obligations, such as some transparency reporting and the internal complaint system. Baseline duties still apply to everyone: contact points, clear T&Cs, and cooperation with authorities. — [Presencis DSA applicability](https://cdn.presencis.com/regulations/dsa/applicability/); [Clym DSA](https://regulations.clym.io/eu-digital-services-act-dsa); [Bird & Bird](https://www.twobirds.com/en/insights/2024/poland/240301-digital-services-act-akt-o-uslugach-cyfrowych-juz-obowiazuje)
- **CCPA/CPRA (California):** applies to for-profit businesses that meet thresholds (for example, about $25M+ revenue, adjusted for inflation, or buying, selling or sharing the data of 100k+ consumers). CPRA treats "precise geolocation" and government ID numbers as "sensitive personal information", which carries a right to limit its use. — [California AG CCPA page](https://oag.ca.gov/privacy/ccpa). This is from background knowledge of the statute; the page was not fetched in this session.

### Inferences
- **Age:** require 18+ for an app that matches strangers to meet in person. Tourlina lists its app as 17+ on the App Store ([App Store listing](https://apps.apple.com/app/id1017254933)). Collecting date of birth for an age gate is justified, but show only age, never the full date of birth. Note that a self-declared date of birth is not verification. Document verification can confirm age.
- **Consent and lawful basis:** use "contract" for core features such as the profile, activities and the contact reveal. Use explicit consent for optional biometric verification (GDPR Art. 9 and Israeli biometric rules) and for precise location. Keep marketing consent separate. Publish a privacy policy that meets Amendment 13's expanded notice duty (purpose, whether providing data is mandatory, recipients).
- **Retention:** delete chat and contact-reveal logs a set time after an activity ends, except where they are kept for safety investigations. Delete verification images at the vendor after a short window. Close deleted accounts within 30 days (the GDPR Art. 12 response window), but keep a hashed block-list entry so banned users can't simply re-register. This lesson is shown by the Couchsurfing Maglio case in Q4.
- **Ratings stored on a deleted user:** decide whether reviews a user wrote about others survive the author's deletion (anonymized) and whether a user can delete reviews about themselves (they should not be able to). Document this in the T&Cs, since it touches erasure rights.
- **DPIA:** run one before launch. It is required under GDPR Art. 35 for systematic location processing combined with matching strangers, and possibly for biometric verification. A DPO under Amendment 13 may be required if the business model involves systematic monitoring. Get Israeli counsel to confirm.
- **EU representative:** a non-EU controller targeting EU users generally must appoint an EU representative under GDPR Art. 27. This is background knowledge; confirm with counsel.

### Gaps
- I could not confirm whether Amendment 13 lists precise location or ID numbers as "data of special sensitivity". Sources conflict or are silent, so check with Israeli counsel or the PPA text.
- I did not verify the exact current CCPA revenue threshold (it is inflation-adjusted, so check the 2026 figure).
- I found no PPA-specific guidance on age limits for social or meetup apps.

## Q3. Identity verification options and costs; how peer apps do it

### Takeaway
Plug-and-play document plus selfie-liveness verification is cheap enough for a startup, at roughly $0.80–$1.50 per check at list prices. Peers increasingly require at least a selfie video match (Tinder Face Check, Couchsurfing). Run verification as a tiered badge, not a hard gate, at the MVP stage. Keep the option to require it before contact details are revealed.

### Cited Findings
- **Stripe Identity:** $1.50 per verification (document plus selfie), with 50 free. — [Stripe Identity](https://stripe.com/gb/identity); [APIScout comparison 2026](https://apiscout.dev/blog/best-identity-verification-apis-2026)
- **Veriff:** smallest self-serve plan reported at $49/month plus $0.80 per verification. — [APIScout](https://apiscout.dev/blog/best-identity-verification-apis-2026). This is an aggregator figure; check on veriff.com.
- **Persona:** modular; reported from about $1–2 per verification, but mostly quote-based with annual terms. — [APIScout](https://apiscout.dev/guides/best-identity-verification-apis-2026); [StartWithIdentity rankings](https://startwithidentity.com/rankings/best-identity-verification-for-startups/)
- **Tinder Face Check:** a short video selfie creates a 3D face scan that is matched to the user's profile photos, and a match earns the "Photo Verified" badge. It has been mandatory for new users in California since June 2025, and in Canada, Colombia, Australia, India and parts of Southeast Asia. It was extended to more US states in October 2025, and Match Group said it would roll it out to its other apps in 2026. Tinder reports a 60% drop in exposure to "bad actors" and a 40% drop in bad-actor reports. — [TechCrunch, 22 Oct 2025](https://techcrunch.com/2025/10/22/tinder-will-require-new-users-in-the-us-to-verify-their-identity-with-a-selfie); [Axios, 30 Jun 2025](https://www.axios.com/2025/06/30/tinder-face-check-safety); [Bitdefender](https://www.bitdefender.com/en-us/blog/hotforsecurity/tinder-face-check-us-states-what-it-means-for-you)
- **Bumble:** photo verification by a selfie that copies a randomly chosen pose. — [Bitdefender summary](https://www.bitdefender.com/en-us/blog/hotforsecurity/tinder-face-check-us-states-what-it-means-for-you)
- **Couchsurfing:** has moved to government-ID plus video-selfie verification, with an automated match done in the app. — [Couchsurfing: Why did verification change](https://support.couchsurfing.org/hc/en-us/articles/50048546692507-Why-Did-Verification-Change); [What is verification](https://support.couchsurfing.org/hc/en-us/articles/49997779463067-What-Is-Verification-and-How-Do-I-Get-Verified)
- **Airbnb:** may require a government ID and a selfie, which are not shared with other users. — [Airbnb Help 3033](https://www.airbnb.co.uk/help/article/3033)
- **Tourlina (women-only travel buddy app):** new users take a selfie holding the word "Tourlina" and the date to prove they are women. The team manually verifies users within about 48 hours, and messaging is only possible after verification. — [Ordinary Traveler](https://ordinarytraveler.com/tourlina-best-app-solo-female-travelers); [JohnnyJet](https://johnnyjet.com/tourlina-app-connect-with-other-female-travelers/)

### Inferences
- **Recommended tiers:** (1) email or phone verified, which is required. (2) Photo-verified by selfie liveness against profile photos, which is cheap and protects against catfishing. (3) ID-verified by document plus selfie through Stripe Identity or Veriff, shown as a badge. Consider requiring tier 2 or 3 for group "managers" or before contact details are revealed.
- Verification works on any country's passport, which solves the global-audience problem that the Israeli ID field creates.
- **Cost at MVP scale:** 1,000 ID checks cost about $800–$1,500. Selfie-only liveness is usually cheaper.

### Gaps
- I found no reliable public information on Hey VINA's verification method.
- I could not confirm current list prices for Onfido (Entrust), Jumio or Yoti. They are enterprise and quote-based, and no public figures turned up in this session.
- I did not confirm whether Tourlina still uses manual selfie checks in 2026.

## Q4. Safety features in meetup, dating and travel-buddy apps, and the incidents behind them

### Takeaway
The spec reveals phone and email as soon as both managers approve, with no in-app chat. That is weaker than industry practice. The standard pattern is in-app chat first, with optional contact sharing later by each user's own choice. It is backed by block and report, share-my-plans, meet-in-public prompts, and ban-evasion controls.

### Cited Findings
- **Couchsurfing / Dino Maglio case:** an Italian policeman who posed as a host drugged and raped a 16-year-old Australian and may have assaulted up to 15 other women. He was sentenced to 6.5 years. After his account was suspended, he re-registered under an alias and carried on. Couchsurfing's stated policy at the time was that a report of attempted sexual assault was not enough on its own to ban a user, and it relied on references. — [The Local Italy, 2015](https://www.thelocal.it/20150318/prison-term-sought-in-italy-couchsurfing-rape-case); [Malay Mail, 2015](https://www.malaymail.com/news/world/2015/02/06/italian-couchsurfing-user-charged-with-drugging-and-raping-australian-15-ot/835727); [Skift](https://skift.com/?p=30024); [The Next Web](https://thenextweb.com/news/man-allegedly-rapes-woman-meeting-couchsurfingcom)
- **Tinder and Noonlight (2020):** users log date details on a "Timeline" that alerts friends, and a panic button alerts Noonlight dispatchers. If the user doesn't respond to a text or call, Noonlight contacts emergency services. — [6abc](https://6abc.com/tinder-dating-app-panic-button-safety-features/5875632/); [Dazed](https://www.dazeddigital.com/science-tech/article/47618/1/dating-app-tinder-launching-a-panic-button-noonlight)
- **Bumble:** Share Date sends date details and location to trusted contacts. It also offers Block & Report (which works even after unmatching), Unmatch (the conversation disappears and the other person can no longer see your profile) and a Safety Center. — [Bumble support: Our safety features](https://support.bumble.com/hc/articles/28537051467293-Our-safety-features); [Bumble Block & Report](https://bumble.com/the-buzz/block-report-bumble); [Bumble Unmatch](https://bumble.com/the-buzz/unmatch-bumble)
- **Tourlina:** women-only and verified before messaging (see Q3). — [Ordinary Traveler](https://ordinarytraveler.com/tourlina-best-app-solo-female-travelers)

### Inferences
- **Change the contact-reveal flow.** After both managers approve, open an in-app group chat. Let each member choose to share their own phone or email, or use masked relay numbers (for example through Twilio Proxy-type services). Don't auto-reveal every member's contacts. As specified, the managers' approval also exposes the non-manager members' contact details without their individual consent, which is a GDPR and Amendment 13 consent and minimization problem.
- **Minimum safety set for the MVP:** block and report on profiles, groups and chats; a moderation queue with a defined SLA; ban-evasion controls (hash of verified document, device fingerprint, phone); meet-in-public tips before the first meetup; "share activity with a trusted contact" (time, place and group members); a link to local emergency numbers.
- **Later:** women-only or same-gender groups as a filter (Tourlina shows demand for this); an optional check-in timer; a Noonlight-style integration where it's available (mainly the US).
- **Lesson from the Maglio case:** credible serious-harm reports should trigger immediate suspension pending review, not reliance on references. Verification is needed so bans can't be evaded by re-registering.
- Background checks (sex-offender registry, as in the US Match Group–Garbo period) are US-centric and legally fraught in the EU and Israel. They are not recommended for a global MVP.

### Gaps
- I didn't find recent (2024–2026) documented incidents tied to travel-buddy apps specifically. Most reported incidents involve Couchsurfing or dating apps.
- I did not verify Bumble's or Tinder's current emergency-button availability by country.

## Q5. Ratings and reviews design

### Takeaway
Keep reviews, but change the trigger and the mechanics. A review should require a meeting confirmed by both sides, not just the deletion of an activity. Hide reviews until both sides submit or a window closes (double-blind). Rate individuals, not only groups. Add reporting of reviews and a right to reply.

### Cited Findings
- **Airbnb:** both parties have 14 days after checkout to review. Each review stays hidden until both are submitted or the window expires, then both publish at once, and they can't be edited afterwards. Airbnb said it introduced this because of fear of retaliation. — [Hostfully 2026](https://www.hostfully.com/blog/airbnb-review-policy/); [VentureBeat](https://venturebeat.com/business/airbnb-tweaks-review-system-so-guests-dont-fear-retaliation-from-hosts/); [Guesty help](https://help.guesty.com/hc/en-gb/articles/9362448128669-How-soon-do-reviews-become-public-on-guest-and-host-listing-profiles)
- **Fradkin, Grewal & Holtz (Marketing Science, 2021), field experiment on Airbnb:** with simultaneous reveal, guests' average ratings were 0.25% lower. Reviews with negative text rose 12% for guests and 17% for hosts. Guest 1-star retaliation against hosts fell 31%. Review rates went up because people were curious to see the other side's review. — [INFORMS Marketing Science](https://pubsonline.informs.org/doi/fpi/10.1287/mksc.2021.1311); [SSRN](https://papers.ssrn.com/abstract=2939064); [INFORMS news release](https://www.informs.org/News-Room/INFORMS-Releases/News-Releases/New-Research-Highlights-Motivations-of-Reviewers)

### Inferences
- **Problems with the spec's trigger:** "Ratings when the activity is deleted as done together" lets one manager declare an activity "done" without proof that a meeting happened. That allows fake or retaliatory reviews. It also ties reviews to deletion, which conflicts with data retention if the activity record disappears.
- **Recommendation:** after the activity's end time, each participant confirms "we met", and only mutually confirmed attendees can review each other. Use a 14-day double-blind window. Review individuals, with an optional group-level summary, so one bad member doesn't damage a whole group. Show a rating count alongside the average (for example, "4.8 · 12 reviews"). Hide averages below a minimum count (for example, under 3). Let users report abusive reviews, let the person reviewed post one public reply, and keep a private "safety feedback" channel that goes to moderators rather than the public profile. That channel captures safety signals people won't write publicly.
- **Fake review safeguards:** one review per pair per activity; reviews only between verified accounts; detection of reciprocal-review rings.

### Gaps
- I didn't locate empirical studies specifically on group-vs-individual ratings in meetup platforms.

## Q6. Location privacy: exact vs approximate display, distance fuzzing

### Takeaway
Show approximate distance and an area (neighbourhood or city) publicly. Reveal the exact meeting point only to approved participants. Compute distances server-side, with snapping and rounding, so trilateration attacks can't recover precise positions.

### Cited Findings
- KU Leuven researchers (2024) found trilateration flaws in six dating apps: Bumble, Hinge, Happn, Grindr, Badoo and Hily. Grindr allowed "exact distance trilateration" to roughly a 111×111 m area. Using "oracle trilateration" against distance filters, they located users to within 2 m on some apps. The apps contacted later changed how their distance filters work. — [TechCrunch, 31 Jul 2024](https://techcrunch.com/2024/07/31/bumble-and-hinge-allowed-stalkers-to-pinpoint-users-locations-down-to-2-meters-researchers-say); [TechRadar](https://www.techradar.com/pro/privacy-flaw-in-top-dating-apps-could-have-revealed-user-location-down-to-2-metres); [Lowyat](https://lowyat.net/2024/328056/researchers-privacy-bug-dating-apps)
- Location data is an explicit identifier under Amendment 13's definition of "identifiable person". — [Baker McKenzie](https://resourcehub.bakermckenzie.com/en/resources/global-data-and-cyber-handbook/emea/israel/topics/key-definitions). CPRA treats precise geolocation as sensitive personal information. — [CA AG CCPA](https://oag.ca.gov/privacy/ccpa)

### Inferences
- **Good part of the spec:** sorting by the activity's origin location rather than the user's live GPS is privacy-friendly, provided the origin is a public meeting point (a café or landmark) and not a hotel or home address. Add UI guidance such as "choose a public place".
- **Implementation:** snap user locations to a grid (for example, geohash at about 1 km) before computing distances. Show rounded buckets ("<1 km", "1–5 km", "5–10 km"). Never return raw coordinates or exact distances in the API. Rate-limit location-filter queries per account to defeat oracle trilateration. Only ask for the browser's precise location with explicit consent, and offer a "set my city manually" alternative.
- Reveal the exact meeting point and time only after approval (in the in-app chat), not on the public listing.

### Gaps
- I didn't find formal EDPB guidance specific to location fuzzing in social apps. The general GDPR minimization principle applies.

## Summary scorecard (keep / change)

| Spec element | Verdict | Recommendation |
|---|---|---|
| National ID number at registration | Change | Remove. Optionally verify any government ID through a vendor and store only the result. |
| Login by ID number + password | Change | Use email or phone + OAuth (Google/Apple), with passkeys later. |
| Full name, DOB, phone, email | Keep with changes | Show first name and age only. Keep DOB private. Phone may be optional if email is verified. |
| Country of origin | Keep, optional | Low risk. Watch for discriminatory filtering. |
| Optional photo/bio | Keep | Add selfie-liveness "Photo Verified" badge. |
| Auto-reveal phone/email after manager approval | Change | In-app chat first, with opt-in individual contact sharing or masked numbers. |
| Profiles show name, photo, age, rating | Keep with changes | Show rating count, hide low-count averages, keep DOB hidden. |
| Ratings on "activity deleted as done" | Change | Mutual "we met" confirmation, 14-day double-blind, individual reviews, report/reply. |
| Proximity by activity origin | Keep | Public meeting points only, rounded distances, server-side fuzzing. |
| Missing: age gate, block/report, moderation, deletion/export, DPIA, DSA notice-and-action | Add | Required baseline before a public launch. |
