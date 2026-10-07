/**
 * Draft legal texts (PLAN.md §4.9). They describe what the app actually does and must be reviewed by
 * privacy counsel before public launch. English only until then; translations follow the reviewed text.
 *
 * LEGAL_VERSION must match the API's `Auth:LegalVersion`: when it changes, everyone is asked to accept again.
 */
export const LEGAL_VERSION = '2026-10-01'

export type LegalDoc = 'terms' | 'privacy' | 'guidelines'

type Section = { heading: string; body: string[] }

export type LegalText = { title: string; summary: string; sections: Section[] }

export const legalDocs: Record<LegalDoc, LegalText> = {
  terms: {
    title: 'Terms of Service',
    summary: 'Travether helps adult travelers who are on the road at the same time meet for activities. You decide whom you meet; we provide the tools and step in when someone breaks the rules.',
    sections: [
      {
        heading: 'Who can use Travether',
        body: [
          'You must be at least 18 and able to agree to these terms. One account per person, in your own name, with a first name others will see.',
          'You are responsible for what happens under your account. Keep your sign-in details to yourself.',
        ],
      },
      {
        heading: 'What Travether is and is not',
        body: [
          'Travether lets you share trips (Vacation Cards), post Activity Plans and join other travelers. We do not organize, guide, insure or check activities, places or people beyond what is shown in the app.',
          'Meeting people you don’t know carries risk. Meet in public places, tell someone you trust where you are going, and leave if you feel unsafe. The Safety center in the app has more tips.',
        ],
      },
      {
        heading: 'Your content',
        body: [
          'You keep the rights to what you post. You allow us to store, show and transmit it as needed to run the service for the people you share it with.',
          'Don’t post anything illegal, anything you have no right to share, or anything that breaks the Community Guidelines.',
        ],
      },
      {
        heading: 'Reviews',
        body: [
          'After a plan, people who confirm they met can review each other. Reviews must be honest and about the meeting. Both reviews are published together, or when the review window closes, so neither side can retaliate.',
        ],
      },
      {
        heading: 'Moderation',
        body: [
          'Anyone can report a profile, card, plan, message or review. Moderators may remove content and suspend accounts that break these terms or the guidelines. When we remove something or suspend an account, we tell the person affected why and how to appeal.',
        ],
      },
      {
        heading: 'Leaving',
        body: [
          'You can delete your account at any time in Settings. What happens to your data is described in the Privacy Policy.',
        ],
      },
      {
        heading: 'Liability',
        body: [
          'Travether is provided as is. To the extent the law allows, we are not liable for what users do on or off the platform, including at activities arranged through it. Nothing here limits rights you have under consumer protection law.',
        ],
      },
      {
        heading: 'Changes',
        body: ['When these terms change in a meaningful way, the app asks you to read and accept the new version before you continue.'],
      },
    ],
  },
  privacy: {
    title: 'Privacy Policy',
    summary: 'We collect only what Travether needs to work, show precise locations only to people you approved, and let you download or delete your data at any time from Settings.',
    sections: [
      {
        heading: 'What we collect',
        body: [
          'Account: email, password (stored only as a hash) or the Google or Apple account you sign in with, full name, first name, date of birth, country and phone number if you add one.',
          'Profile: photo, bio, languages and interests you choose to add.',
          'Trips and plans: the cards and plans you create or join, including meeting points, join requests, chat messages, "did you meet?" answers and reviews.',
          'Technical: a random device identifier and your IP address, used to keep accounts secure, limit abuse and enforce suspensions; push notification addresses for devices where you turned push on.',
          'We do not collect national ID numbers or track your live location.',
        ],
      },
      {
        heading: 'Who sees what',
        body: [
          'Everyone: your first name, age, country flag, photo, bio, languages, interests, badges and published reviews, and the area and rough distance of public plans.',
          'People you are approved to travel or meet with: your full name and the exact meeting point.',
          'Only you: your email, phone number, date of birth and the contents of your settings. You can share your phone number in a chat yourself.',
          'Moderators see reported content and what they need to decide on a report.',
        ],
      },
      {
        heading: 'Why we use it (legal bases)',
        body: [
          'To provide the service you signed up for (contract): your account, profile, trips, plans, chats, notifications and reviews.',
          'To keep the community safe (legitimate interest and legal obligations): rate limits, reports, moderation, and records of suspended email addresses, phone numbers and devices, stored as one-way hashes.',
          'Product analytics only with your consent, which you can withdraw at any time in Settings → Who sees what.',
        ],
      },
      {
        heading: 'Service providers',
        body: [
          'Render (hosting, EU) and Neon (database, EU), Cloudinary (photos), Resend (email), and the browser push services of Google, Apple, Mozilla and Microsoft for notifications. Meeting-point search sends your search text to a Photon (OpenStreetMap) geocoder. Each processes data only on our instructions.',
        ],
      },
      {
        heading: 'How long we keep it',
        body: [
          'Your account data: until you delete your account. Notifications: 90 days. One-time sign-in codes: one day after they expire. Device identifiers: 180 days after last use. Records of consent and of moderation decisions are kept as long as we may need to show them.',
        ],
      },
      {
        heading: 'Deleting your account',
        body: [
          'In Settings → Delete account. Your name, email, phone, birth date, photo, bio, sign-in methods, devices, notifications, blocks and reviews about you are deleted at once. Trips you own pass to a co-admin or member, or are deleted if nobody else is on them; plans you host that haven’t happened are cancelled.',
          'Messages you sent in shared chats and reviews you wrote stay with the people they were sent to, shown as from a deleted account, because they are part of those people’s conversations and ratings.',
        ],
      },
      {
        heading: 'Your rights',
        body: [
          'You can see and correct your data in the app, download all of it as a file (Settings → Download my data), withdraw consent, object to processing and delete your account. You may also complain to your data protection authority, including the Israeli Privacy Protection Authority or the authority in your EU country.',
        ],
      },
      {
        heading: 'Contact',
        body: ['Questions about privacy: privacy@travether.app.'],
      },
    ],
  },
  guidelines: {
    title: 'Community Guidelines',
    summary: 'Be the travel companion you would want to meet.',
    sections: [
      {
        heading: 'Be real',
        body: ['Use your own name and a recent photo of yourself. One account per person. No fake profiles, no impersonation, no one under 18.'],
      },
      {
        heading: 'Be respectful',
        body: [
          'No harassment, hate, threats or sexual advances that aren’t welcome. "No" ends the conversation. Don’t share other people’s private details.',
        ],
      },
      {
        heading: 'Be honest',
        body: ['Describe plans as they are. Show up, or leave or cancel in good time. Reviews are about the meeting, not the person’s looks or background.'],
      },
      {
        heading: 'No selling, no scams',
        body: ['Travether is not for advertising, paid tours, dating, or asking for money. Report anyone who asks you to pay or move to another app early in a suspicious way.'],
      },
      {
        heading: 'Stay safe',
        body: ['Meet in public places, especially the first time. Trust your instincts. If something is wrong, use Report or Block; in an emergency, call local emergency services first.'],
      },
      {
        heading: 'What happens when rules are broken',
        body: ['Moderators review every report. Content can be removed and accounts suspended. You are told why, and you can reply to the email to appeal.'],
      },
    ],
  },
}
