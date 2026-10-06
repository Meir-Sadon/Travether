import { Route, Routes } from 'react-router'
import { AuthProvider } from './auth/AuthContext'
import { RequireAuth } from './auth/RequireAuth'
import { AppShell, BareShell } from './layout/AppShell'
import { DesignSystemPage } from './pages/DesignSystemPage'
import { StatusPage } from './pages/StatusPage'
import { CardPreviewScreen } from './screens/CardPreviewScreen'
import { ChatScreen } from './screens/ChatScreen'
import { DiscoverScreen } from './screens/DiscoverScreen'
import { EditProfileScreen } from './screens/EditProfileScreen'
import { ForgotPasswordScreen } from './screens/ForgotPasswordScreen'
import { HomeScreen } from './screens/HomeScreen'
import { InboxScreen } from './screens/InboxScreen'
import { LandingScreen } from './screens/LandingScreen'
import { LoginScreen } from './screens/LoginScreen'
import { NotFoundScreen } from './screens/NotFoundScreen'
import { NotificationsScreen } from './screens/NotificationsScreen'
import { PersonScreen } from './screens/PersonScreen'
import { PlanScreen } from './screens/PlanScreen'
import { ProfileScreen } from './screens/ProfileScreen'
import { ReviewScreen } from './screens/ReviewScreen'
import { SettingsScreen } from './screens/SettingsScreen'
import { SignupScreen } from './screens/SignupScreen'
import { TripScreen } from './screens/TripScreen'

/**
 * Screens are numbered as in PLAN.md §5. Visitors can open the landing page, Discover, shared
 * cards and plan previews (PLAN.md §3); everything else sends them to sign up first.
 */
export default function App() {
  return (
    <AuthProvider>
      <Routes>
        <Route element={<AppShell />}>
          <Route path="discover" element={<DiscoverScreen />} />
          <Route element={<RequireAuth />}>
            <Route index element={<HomeScreen />} />
            <Route path="inbox" element={<InboxScreen />} />
            <Route path="profile" element={<ProfileScreen />} />
          </Route>
        </Route>
        <Route element={<BareShell />}>
          <Route path="welcome" element={<LandingScreen />} />
          <Route path="signup" element={<SignupScreen />} />
          <Route path="login" element={<LoginScreen />} />
          <Route path="forgot" element={<ForgotPasswordScreen />} />
          <Route path="c/:slug" element={<CardPreviewScreen />} />
          <Route path="plans/:planId" element={<PlanScreen />} />
          <Route path="people/:userId" element={<PersonScreen />} />
          <Route element={<RequireAuth />}>
            <Route path="trips/:tripId" element={<TripScreen />} />
            <Route path="plans/:planId/review" element={<ReviewScreen />} />
            <Route path="inbox/:chatId" element={<ChatScreen />} />
            <Route path="notifications" element={<NotificationsScreen />} />
            <Route path="settings" element={<SettingsScreen />} />
            <Route path="profile/edit" element={<EditProfileScreen />} />
          </Route>
          <Route path="*" element={<NotFoundScreen />} />
        </Route>
        <Route path="design" element={<DesignSystemPage />} />
        <Route path="status" element={<StatusPage />} />
      </Routes>
    </AuthProvider>
  )
}
