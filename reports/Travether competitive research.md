# Travether can win on density, not features

Travether's core idea is that vacation groups find other groups heading to the same country and team up for specific, time-bound activities. No competitor reviewed does exactly this, but every piece of it already exists somewhere. Hostelworld has user-created "LinkUps" and "see who's going". Meet2Trip lets creators approve who joins their cruise-port excursions. Couchsurfing Hangouts let travelers join live activities, and Timeleft runs small-group meetups between strangers. **The concept is novel as a combination, not as a building block, so features will not protect it. The only lasting advantage is being dense in a few destinations and building trust data there.**

The history of the category points the same way. Standalone travel-buddy apps have died of empty markets and no revenue (Twigmore, LocalUncle, Wanderlust Society, Desti), or by charging for the community itself (Couchsurfing's 2020 paywall). The apps that survived attached social features to bookings: Hostelworld has 3.4M+ social members, and they book about twice as often as other users.

Measured against these lessons and current privacy law, the spec has six structural weaknesses:

1. Login by Israeli national ID, which excludes most travelers worldwide and carries regulatory risk.
2. Automatic reveal of phone and email instead of in-app chat.
3. Matching at country level without filtering by date window.
4. Liquidity limited to group-to-group only.
5. Ratings triggered when one side deletes a plan.
6. No public surface for discovery or growth.

Each has a well-tested fix. The recommended MVP is a Hebrew-first, mobile-first PWA launched in two or three Israeli-backpacker corridors. It replaces ID login with email, phone or OAuth, and adds in-app chat, date-and-city matching, open plans and double-blind reviews. Revenue comes later, from activity-booking commissions rather than paywalls.

## Twenty apps tried this, and the scaled ones sell bookings

The market is crowded and fragmented, with no category leader. Direct "travel buddy" matchers pair an individual with a trip using destination, dates and interests:

- **GAFFL** (launched 2017) claims **90,000+ trips initiated across 190+ countries**. It sells a "GAFFL Unlimited" subscription with unlimited connections and hotel discounts ([App Store](https://apps.apple.com/us/app/id1470182395)).
- **Travel Buddy India** claims millions of users and **60,000+ premium subscribers** on about **$300K of total funding**. It has moved into selling flights, hotels and group tours ([fundediq](https://fundediq.co/travel-buddy-beatravelbuddy-com-funding/); [Dealroom](https://app.dealroom.co/companies/travel_buddy_india)).
- **Tourlina** is women-only and checks profiles manually before anyone can message ([trespot](https://trespot.com/travel-buddy-apps.html)).
- **Fairytrail** is a travel dating app with booking partnerships ([DatingNews](https://www.datingnews.com/movers-and-shakers/fairytrail-dating-app-pioneers-travel-matches-and-dates/)).
- A 2025–26 wave of new entrants (roammate, Trespot, Bubblic, a relaunched Backpackr, YatriBhet) compete mainly through "best travel buddy app" SEO articles that often rank themselves first ([roammate](https://roammate.com/blog/best-travel-companion-apps-2026/); [mwm.ai](https://mwm.ai/apps/backpackr/6757887080)). This points to low switching costs and no single winner.

The players with real scale run a different model. **Hostelworld's social layer**, launched in 2022, has **3.4M+ members**. Messaging grew **81% year over year** in 2025, social members book about **2x as often**, and they account for roughly **85% of bookings** ([Hostelworld FY2025 RNS](https://shareprices.com/rns/preliminary-results-for-the-year-ended-31-dec-2025-sblia4zbb33hq1w/)). Its features include:

- City and hostel chat rooms.
- "See who's going".
- LinkUps that hostels or travelers create and that open 14 days before arrival.
- A "global trip plan marketplace" ([Hostelworld LinkUps](https://www.hostelworld.com/linkups); [PhocusWire](https://www.phocuswire.com/hostelworld-social-strategy-global-trip-plan-marketplace)).

Hostelworld is the most dangerous incumbent for Travether's pitch. Its limit is that it is tied to hostel stays and a young audience.

**Travello** shows the other common path. It claimed 350K users in 2018 and raised A$1.4M, then A$5M, then a A$10M Series B. It pivoted to selling tours (40,000+), and TWAI acquired it in June 2025. A third-party tracker now estimates about 11.7K MAU ([Arival](https://arival.travel/how-travello-raised-10-million-grew-7x-coming-out-of-the-pandemic/); [EIN Presswire](https://www.einpresswire.com/article/826249929/twai-acquires-travello-to-enter-200b-tours-activities-market-and-expand-global-consumer-reach); [AppGoblin](https://appgoblin.info/apps/924774827)). **JoinMyTrip** takes a commission on leader-run group trips with an average basket of about €1,100 ([CB Insights](https://www.cbinsights.com/company/joinmytrip); [affi.io](https://affi.io/m/joinmytrip-uk)).

| Product | Matching basis | Unit | Contact model | Monetization |
|---|---|---|---|---|
| GAFFL | Destination + dates | Individual → trip | In-app, limited on free tier | Subscription + hotel discounts |
| Travel Buddy (India) | Destination, "AI Buddy Finder" | Individual + group tours | In-app | Premium tiers + bookings |
| Tourlina | Destination, dates, interests; women only | 1:1 | In-app after verification | Not found |
| Hostelworld social | City/hostel + dates | Individual + LinkUps | Chat rooms + DMs | Booking commission |
| Travello | Interests, location | Individual / events | Feed + chat | Tour commission |
| Meet2Trip | Same cruise port + same day | Party → excursion/taxi | Chat; creator approves joiners | Not found |
| Timeleft | Personality quiz → table of 6 | Small group | Revealed at dinner | Per-dinner / subscription |
| **Travether (spec)** | **Same country, sorted by origin distance** | **Group → group, per activity** | **Phone/email reveal after mutual approval** | **Undefined** |

Sources: [Travolution](https://www.travolution.com/news/travel-sectors/accommodation/hostelworld-sets-out-to-power-social-connections-with-the-solo-system), [AlternativeTo](https://alternativeto.net/software/meet2trip/about/), [AZFamily](https://www.azfamily.com/2025/01/30/want-meet-new-people-app-organizes-dinners-with-strangers-phoenix-area).

### Why the dead ones died

Almost every failure was an empty-market or no-revenue failure:

- **Twigmore** shut down after two years ([PhocusWire](https://www.phocuswire.com/End-of-the-road-for-social-travel-app-Twigmore)).
- **LocalUncle** said it "hadn't seen the expected uptake" ([PhocusWire](https://www.phocuswire.com/LocalUncle-calls-uncle-ceases-operations)).
- **Wanderlust Society**, founded by ex-Amazon staff, ran out of runway for lack of growth in 2018 ([GeekWire](https://www.geekwire.com/2018/end-road-wanderlust-society-travel-startup-created-amazon-vets-shutting/)).
- **Desti** raised $2M but could not turn engagement into revenue, because users booked on established platforms ([Failory](https://www.failory.com/cemetery/desti)).
- **Dopplr** sold to Nokia for about $20M and was shut down four years later ([Failory](https://failory.com/cemetery/dopplr)).

Couchsurfing is the instructive exception. Its sudden 2020 membership paywall caused "widespread anger" on top of existing safety complaints, and a nonprofit splinter, Couchers.org, grew out of it ([LeftEast](https://lefteast.org/gift-or-data-platform-couchsurfing/); [Couchers](https://couchers.org/issues/profit-and-incentives/)).

User reviews across the category repeat the same complaints:

- Fake and scam profiles, including AI-generated photos and crypto scams ([Trustpilot](https://ie.trustpilot.com/review/www.tripgiraffe.com)).
- Romantic approaches toward users who signed up for "companions" ([DatingScout](https://www.datingscout.com/fairytrail/review)).
- Paywalls on replies: "potential travel companions contact you, but you can't reply unless you pay" ([SmartCustomer](https://www.smartcustomer.com/reviews/travelfriend.online)).
- Empty markets. Roundups advise using several apps and then "double down on the one with active matches at your destination" ([trespot](https://trespot.com/travel-buddy-apps.html)).

Travether's design already answers some of these. Groups instead of individuals and purpose-framed activities reduce dating dynamics, and post-activity ratings add trust. Empty markets are its biggest exposure: it needs density per country, per date window, and within reach of an activity's origin, all at the same time.

## Successful apps look calm, photo-led and list-plus-map, not swipe

Category leaders share a visual discipline:

- **Airbnb** reserves one saturated accent (Rausch, #FF385C) for primary CTAs and the wishlist heart on a white canvas. It uses a rounded custom variable sans at weights 500–700 and a soft ~14px corner radius. Analysts describe the interface as "designed for browsing, not commanding" ([getdesign.md](https://getdesign.md/design-md/airbnb/preview)).
- **Hinge** pairs an editorial serif (Tiempos) with a friendly sans (Modern Era) and keeps its illustrations about 80% black and white ([Hinge brand](https://hinge.co/brand)).
- **Tinder's** Porto Rocha rebrand moved to an expressive serif, a wider palette and a "knowledgeable friend" voice, with mixed reception ([DesignRush](https://news.designrush.com/tinder-gen-z-rebrand-porto-rocha); [Creative Bloq](https://www.creativebloq.com/design/branding/is-the-tinder-rebrand-a-total-success-not-everyone-is-so-sure)).
- **Meetup's** rebrand went "bright, bold, energetic, and a little quirky" ([LogoLounge](https://www.logolounge.com/news/meetup-grows-up-an-interview-with-stefan-sagmeister)).

Interaction patterns are moving from 1:1 swiping toward groups, events and calendars. **Bumble BFF's September 2025 relaunch** made a Groups tab with chat rooms, hangout planning and an in-app calendar its centerpiece ([TechCrunch](https://techcrunch.com/2025/09/18/bumble-bffs-revamped-app-is-here-focusing-on-friend-groups-and-community-building)). Other patterns map directly onto Travether's needs:

- **Hostelworld** shows "who's going" and attendee counts ("38 people going"), which works as social proof.
- **Polarsteps** uses secret links that let people view a trip without an account ([Polarsteps](https://support.polarsteps.com/article/265-what-is-travel-together)).
- **Wanderlog** shows trips as route-line maps ([App Store](https://apps.apple.com/us/app/wanderlog/id1476732439)).

Swiping fits Travether badly. Activities are bound to a time and place and need comparing side by side, and swiping carries dating connotations that hurt the sense of safety. The right pattern is a list sorted by distance, with a map toggle and filter chips.

Onboarding should be short. **Timeleft's unskippable personality quiz was criticized as needless friction** ([Pratt IxD](https://ixd.prattsi.org/2025/02/design-critique-timeleft-app/)). Ask for name, photo, home country and languages, then grow the profile gradually.

A mobile-first PWA is viable but has one trap. **iOS web push works only after the user adds the app to the Home Screen, and permission must be requested from a tap** ([OneSignal](https://documentation.onesignal.com/docs/web-push-for-ios); [WebKit](https://webkit.org/blog/13878/web-push-for-web-apps-on-ios-and-ipados)). Push opt-in medians are about **81% on Android and 51% on iOS** ([Pushwoosh](https://www.pushwoosh.com/blog/push-notification-benchmarks/)). Prompt the install right after a moment of clear value, such as sending a first join request.

RTL must be built in from day one:

- Mirror the layout and directional icons, but never numbers ([Material Design](https://m1.material.io/usability/bidirectionality.html)).
- Use a variable font that covers both Hebrew and Latin, such as Rubik, Heebo or Assistant ([Google Fonts](https://fonts.google.com/specimen/Assistant/about)).
- Avoid low-contrast "glass" surfaces. Some iOS 26 Liquid Glass beta screens measured about **1.5:1 contrast against WCAG's 4.5:1** ([Infinum](https://infinum.com/blog/apples-ios-26-liquid-glass-sleek-shiny-and-questionably-accessible/)).

### Three candidate style directions

These are proposals derived from the patterns above. The hex values are starting points that must be contrast-checked.

| | A. Warm Trust | B. Editorial Explorer | C. Expressive Adventure |
|---|---|---|---|
| Model | Airbnb | Hinge, Polarsteps | Meetup, Hostelworld, Material 3 Expressive |
| Palette | White canvas, #222 ink, one "sunset coral" accent (~#FF5A4E) for primary CTAs only, teal (~#0E9F8E) for verified | Warm off-white #FAF7F2, deep ocean #1F3A5F primary, terracotta #D9653B accents, about 80/20 neutral to color | Electric blue #2F5BFF, plus sunshine, coral and mint as activity-category colors; dark mode first-class |
| Type | Rubik or Heebo, 500/600/700 | Display serif headings (check Hebrew coverage, e.g. Frank Ruhl Libre) + Assistant/Heebo UI | Rubik 700–900 headings, 400–500 body |
| Components | Photo-led cards, 14–16px radius, soft shadow, avatar stack, date chip | Full-bleed destination photography, line illustrations, 12px radius, route maps | 20–24px radius, color-coded purpose chips, springy request/approve animations, "boarding pass" Vacation Card with QR |
| Tone | Calm, "travel with people you can trust" | Thoughtful, curious, adult | Playful, emoji-friendly, "let's go!" |
| Best for | When safety is the main adoption barrier | 25–45 travelers and couples | Post-army backpackers / Gen Z; risks lower perceived trust |

Direction A is the safest default for a product whose core act is meeting strangers. A hybrid also works: A's discipline with C's category chips and approval animations. Whichever direction wins, a few elements stay fixed:

- One "Verified" badge with one meaning.
- Approval state shown visibly on the card: Requested → Approved by them → Approved by you → Unlocked.
- A list sorted by distance, with a map toggle.
- Shareable cards that render a public preview for non-users.

## The spec's identity and contact model needs the biggest rework

### National ID as registration and login

The national ID field fails on two counts. First, most travelers worldwide have no Israeli ID. Second, regulators treat ID numbers as high-risk data:

- Israel's Privacy Protection Authority tells businesses to minimize processing of national IDs and to prefer less invasive ways to verify identity ([Pearl Cohen](https://www.pearlcohen.com/israeli-privacy-regulator-says-businesses-should-minimize-their-processing-of-national-ids/)).
- **Amendment 13 (in force since 14 August 2025)** names "identification number" and location data as identifiers ([Baker McKenzie](https://resourcehub.bakermckenzie.com/en/resources/global-data-and-cyber-handbook/emea/israel/topics/key-definitions)). It also brings reported administrative fines of up to **NIS 320,000 per violation** ([Clym](https://www.clym.io/blog/amendment-13-israels-updated-privacy-protection-law-and-what-businesses-must-do-now)).
- GDPR Art. 87 lets EU states impose stricter rules on national identification numbers ([GDPR Art. 87](https://gdpr-text.com/es/read/article-87/)).

No comparable app uses an ID number as a login. Couchsurfing and Airbnb verify a photo of any government ID plus a selfie, and they never show the ID to other users ([Couchsurfing](https://support.couchsurfing.org/hc/en-us/articles/50048521645211-What-Types-of-ID-Are-Accepted-for-Verification); [Airbnb](https://www.airbnb.co.uk/help/article/3033)).

The fix:

- **Log in with email or phone, plus Google and Apple sign-in. Add passkeys later.**
- **Treat verification as a tiered badge:**
  1. Email or phone verified (required).
  2. Photo-verified, by matching a selfie liveness check to profile photos.
  3. ID-verified, through a vendor such as Stripe Identity at **$1.50 per check** or Veriff at about **$0.80 plus $49/month** ([Stripe](https://stripe.com/gb/identity); [APIScout](https://apiscout.dev/blog/best-identity-verification-apis-2026)).

With a vendor, Travether stores only the result, never the document number. Verification at this level visibly works elsewhere: Tinder's mandatory Face Check reportedly cut exposure to bad actors by **60%** and bad-actor reports by **40%** ([TechCrunch](https://techcrunch.com/2025/10/22/tinder-will-require-new-users-in-the-us-to-verify-their-identity-with-a-selfie)).

Collect date of birth for an 18+ age gate, but show only age. Make country of origin optional.

### Phone and email reveal

The reveal after mutual approval is the riskiest element in the spec. Every competitor keeps the first conversation in-app, and messaging is Hostelworld's main engagement driver. As written, approval by the two group creators would also expose the contact details of non-creator group members who never consented individually. That creates a consent and data-minimization problem under GDPR and Amendment 13.

The Couchsurfing Dino Maglio case shows the stakes. A host who drugged and raped a 16-year-old guest re-registered under an alias after being suspended, and the company's policy then did not ban users on a single attempted-assault report ([The Local](https://www.thelocal.it/20150318/prison-term-sought-in-italy-couchsurfing-rape-case); [Skift](https://skift.com/?p=30024)).

The recommended flow:

- After mutual approval, open an **in-app chat between the two groups**.
- Each member can choose to share their own WhatsApp or phone number.
- Exact meeting points appear only inside that chat.

Then add the baseline every peer ships: **block and report, a moderation queue, ban-evasion controls tied to verification, meet-in-public tips, and "share this plan with a trusted contact"** (as in Bumble's Share Date ([Bumble](https://support.bumble.com/hc/articles/28537051467293-Our-safety-features))).

### Location privacy

Sorting by the activity's origin rather than the user's live GPS is a privacy strength worth keeping, provided the origin is a public place and not a hotel. Distances still leak. **KU Leuven researchers located users of six dating apps to within 2 meters through trilateration** ([TechCrunch](https://techcrunch.com/2024/07/31/bumble-and-hinge-allowed-stalkers-to-pinpoint-users-locations-down-to-2-meters-researchers-say)). To prevent that:

- Snap locations to a grid of roughly 1 km before computing distance.
- Show distance buckets such as "<1 km" or "1–5 km".
- Never return raw coordinates from the API.
- Rate-limit distance queries.

### Ratings

Ratings are a real differentiator, since no competitor reviewed prominently stores post-activity reputation. The trigger is the problem. If ratings fire when one creator deletes a plan as "done together", either side can claim a meeting that never happened, and reviews get tied to deleting data. Airbnb's **double-blind system** is the proven alternative: both sides have 14 days, and reviews publish together. In a field experiment it raised review rates, **increased negative-text reviews by 12–17%** (more honesty), and **cut retaliatory 1-star reviews by 31%** ([Fradkin, Grewal & Holtz, Marketing Science](https://pubsonline.informs.org/doi/fpi/10.1287/mksc.2021.1311)).

For Travether:

- After the plan's end time, prompt both sides to confirm "we met".
- Let only mutually confirmed participants review each other.
- Rate individuals, with an optional group summary.
- Show the count alongside the average, and hide averages below about three reviews.
- Let the reviewed person post one public reply, and give users a private safety-feedback channel to moderators.

### Matching and group mechanics

Activity plans carry a date, but discovery ignores date overlap and city. Two groups in Thailand three months apart, or 800 km apart, count as matches. Competitors match on destination plus timeline (Tourlina) or city plus arrival dates (Hostelworld LinkUps) ([Tourlina](https://apps.apple.com/us/app/app/id1017254933); [Hostelworld](https://www.hostelworld.com/linkups)). Changes:

- Add a date window with flexibility, such as ±2 days.
- Match on destination city or region with a radius, and keep distance from the origin as the secondary sort.
- Notify only when both date and place overlap.
- Add capacity and spots-left fields.
- Use activity templates: trek, jeep or transport, dinner, nightlife, tour, beach day.

The group-to-group rule also halves liquidity, because both creators must exist and act. Two fixes:

- Let co-admins send requests on the group's behalf.
- Allow **"open plans"** that individuals or small groups can join directly, up to a cap.

### Cold start and monetization

Every comparable platform started dense in one place:

- Tinder seeded itself at USC parties ([Penguin](https://www.penguin.co.uk/books/440824/the-cold-start-problem-by-chen-andrew/9781529190083)).
- Timeleft failed in Paris before succeeding in Lisbon with one fixed weekly ritual ([CBC](https://www.cbc.ca/1.7391333)).
- Hostelworld seeds LinkUps through the hostels themselves.

Travether's obvious atomic network is Israeli post-army backpackers, **about 40,000 a year**, who cluster around Chabad houses in South America and on known routes ([Israel21c](https://archive.israel21c.org/israeli-backpackers-volunteer-in-india-ethiopia-argentina/); [Lubavitch.com](https://lubavitch.com/lost-in-the-andes-found-by-chabad)). They already look for partners informally, for example in Lametayel's "looking for travel partners" forum posts ([Lametayel](https://www.lametayel.co.il/tips/l0wkm8)). The volume figure is older and needs current confirmation.

Seed two or three corridors first: Peru–Bolivia, Thailand and northern India. In each:

- Pre-create recurring templated plans that fill a jeep or taxi, such as the Uyuni tour or the Salkantay trek.
- Recruit ambassadors.
- Let hostels, Chabad houses and operators post "open seats".
- Publish indexable public destination pages and WhatsApp-shareable plan cards, so the app is useful before anyone signs up.

Monetization should come in this order:

1. A **free core**. Never put contact, safety or basic matching behind a paywall, given Couchsurfing's backlash ([Couchers.org](https://couchers.org/issues/profit-and-incentives/)).
2. **Affiliate "book this together"** on templated plans. GetYourGuide's affiliate commission is about **8%**, negotiable to 10–12% ([Affilimate](https://affilimate.io/programs/getyourguide-affiliate-program/)).
3. **Sponsored "open seats" listings** from operators and guesthouses.
4. Only later, a **convenience premium tier**.

Timeleft shows subscriptions of about $20/month work when the company curates the experience itself ([nexspark](https://nexspark.org/journal/timeleft-review)). That figure comes from a blog and is unverified, and the model does not carry over to a peer community.

## The MVP ships trust and density first; AI and native apps wait

| Area | MVP (before public launch) | Phase 2 | Phase 3 |
|---|---|---|---|
| Identity | Email/phone + Google/Apple login; OTP; 18+ gate; show age not DOB | Selfie "Photo Verified" badge; passkeys | ID verification badge (Stripe/Veriff), optionally required for creators or before contact sharing |
| Matching | Date window + city/region radius; origin-distance sort; templates; capacity | Map view; language/budget/age-range tags; optional women-only plans | AI suggested plans and compatibility |
| Groups | Vacation Card with optional dates, share link + QR, public preview; co-admins; open plans for individuals | Waitlists; recurring plans (weekly Shabbat dinner, pub crawl) | Operator accounts |
| Communication | In-app plan chat and group-to-group chat after approval; opt-in WhatsApp share; exact location only in chat | Shared photo album (viral loop) | Masked relay numbers |
| Safety | Block/report, moderation queue, ban evasion, meet-in-public tips, share-with-trusted-contact, emergency numbers | Check-in timer ("I'm back") | Panic-button integrations where available |
| Ratings | "We met" confirmation, 14-day double-blind, individual reviews, count + minimum threshold, reply, private safety feedback | Badges ("5 activities done together") | Review-ring detection |
| Notifications | Real-time push + email for requests/approvals; daily digest for new matches; frequency controls; guided iOS Home Screen install | WhatsApp Business notifications | Native wrapper if iOS opt-in lags |
| Growth | 2–3 beachhead corridors, ambassadors, seeded plans, indexable destination pages, OG share cards | Hostel/Chabad/operator "open seats" partners | Expand worldwide |
| Revenue | None (free) | Estimated cost per person; GetYourGuide/Viator affiliate | Sponsored listings; convenience premium |
| Compliance | Privacy policy per Amendment 13, deletion/export, retention schedule, DPIA, DSA notice-and-action | EU representative (confirm with counsel) | — |
| Platform | Hebrew-first RTL + English PWA (e.g. Next.js + Supabase/PostGIS) | Splitwise-style cost ledger | Native apps |

The compliance items come from the legal baseline:

- GDPR Art. 35 calls for a DPIA when location processing is combined with matching strangers ([GDPR](https://gdpr-info.eu/)).
- The DSA requires notice-and-action and statements of reasons even for small platforms, with partial exemptions for micro and small enterprises ([Presencis](https://cdn.presencis.com/regulations/dsa/applicability/)).
- Israeli counsel should confirm whether Amendment 13's DPO and database-registration duties apply once the user base passes 10,000.

### What remains uncertain

Several gaps in the evidence matter for these choices:

- No published match-to-meet conversion data exists for travel-buddy apps.
- App store ratings and 2026 status for Tourlina, GAFFL and Fairytrail could not be verified.
- Whether Hostelworld's trip-plan marketplace already supports joining someone else's plan is unconfirmed. If it does, Travether's differentiation narrows further.
- Whether Amendment 13 classifies precise location as "data of special sensitivity" is unresolved.
- None of the style directions has been user-tested.

## Conclusion

The research changes how Travether should think about what makes it distinctive. The group-to-group mechanic, the activity structure and the proximity sort are good product choices, but rivals with millions of users could copy them in a quarter. Three things are hard to copy:

- **Density** in the routes where Israeli backpackers already look for jeep and trek partners.
- **A reputation graph** built from mutually confirmed, double-blind reviews.
- **A trust posture** that is visibly safer than "here is a stranger's phone number".

The spec's instinct to require strong identity is right, and the tool it picked is wrong. A national ID login excludes most users and creates a breach target. Vendor-verified badges deliver the same trust globally without Travether holding the ID data. The order of work matters just as much: launch narrow, safe and free, then earn revenue on the activities people book together rather than on access to each other.

## Decisions for the founder

1. **Target market at launch:** Israeli travelers first (Hebrew-first RTL, 2–3 beachhead corridors), or global English from day one? Which corridors?
2. **Identity:** drop the national ID as a field and as the login, in favor of email/phone + Google/Apple? Which verification tier is required at signup, for group creators, and before contact sharing?
3. **Contact model:** in-app chat with opt-in individual WhatsApp/phone sharing, or keep the automatic phone/email reveal? (Recommended: chat.)
4. **Matching unit:** strictly group-to-group, or also allow open plans that individuals can join with a capacity cap?
5. **Who can act for a group:** creator only, or co-admins and members (with creator confirmation)?
6. **Matching granularity:** add a required date window and city/region radius on top of country?
7. **Ratings trigger and visibility:** mutual "we met" confirmation and a 14-day double-blind window instead of rating-on-delete? Individual ratings, group ratings, or both? Minimum count before an average shows?
8. **Profile exposure:** first name + age + country flag publicly, with full name, DOB, phone and email always private?
9. **Location display:** distance buckets and area publicly, exact meeting point only after approval?
10. **Safety baseline at launch:** which items from block/report, moderation SLA, trusted-contact sharing and women-only plans are must-haves?
11. **Notifications:** real-time for requests/approvals plus a daily digest for matches? Add WhatsApp notifications in phase 2?
12. **Platform:** mobile-first PWA with a guided Home Screen install, with native apps deferred?
13. **Monetization path:** free core plus activity-booking affiliate, then sponsored listings, with no paywall on contact or matching?
14. **Visual direction:** A (Warm Trust), B (Editorial Explorer), C (Expressive Adventure), or an A+C hybrid? Test clickable mockups with target travelers first?
15. **Legal setup:** engage Israeli privacy counsel for an Amendment 13/GDPR review, a DPIA and a check of the "Travel Together" name (Polarsteps uses "Travel Together" as a feature name) before launch?
