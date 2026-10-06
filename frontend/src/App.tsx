import { Route, Routes } from 'react-router'
import { AppShell, BareShell } from './layout/AppShell'
import { DesignSystemPage } from './pages/DesignSystemPage'
import { StatusPage } from './pages/StatusPage'
import { ChatScreen } from './screens/ChatScreen'
import { DiscoverScreen } from './screens/DiscoverScreen'
import { HomeScreen } from './screens/HomeScreen'
import { InboxScreen } from './screens/InboxScreen'
import { LandingScreen } from './screens/LandingScreen'
import { NotFoundScreen } from './screens/NotFoundScreen'
import { PlanScreen } from './screens/PlanScreen'
import { ProfileScreen } from './screens/ProfileScreen'
import { ReviewScreen } from './screens/ReviewScreen'
import { SettingsScreen } from './screens/SettingsScreen'
import { SignupScreen } from './screens/SignupScreen'
import { TripScreen } from './screens/TripScreen'

/** Phase 0: clickable mockups on dummy data (src/mock). Screens are numbered as in PLAN.md §5. */
export default function App() {
  return (
    <Routes>
      <Route element={<AppShell />}>
        <Route index element={<HomeScreen />} />
        <Route path="discover" element={<DiscoverScreen />} />
        <Route path="inbox" element={<InboxScreen />} />
        <Route path="profile" element={<ProfileScreen />} />
      </Route>
      <Route element={<BareShell />}>
        <Route path="welcome" element={<LandingScreen />} />
        <Route path="signup" element={<SignupScreen />} />
        <Route path="c/:slug" element={<TripScreen preview />} />
        <Route path="trips/:tripId" element={<TripScreen />} />
        <Route path="plans/:planId" element={<PlanScreen />} />
        <Route path="plans/:planId/review" element={<ReviewScreen />} />
        <Route path="inbox/:chatId" element={<ChatScreen />} />
        <Route path="settings" element={<SettingsScreen />} />
        <Route path="*" element={<NotFoundScreen />} />
      </Route>
      <Route path="design" element={<DesignSystemPage />} />
      <Route path="status" element={<StatusPage />} />
    </Routes>
  )
}
