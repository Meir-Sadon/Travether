import { HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from '@microsoft/signalr'
import { useEffect, useRef } from 'react'
import type { ChatEvent } from './types'

/** Server pushes: a chat message, or a new in-app notification. */
export type HubEvent = { kind: 'message'; event: ChatEvent } | { kind: 'notification'; type: string }

type Listener = (event: HubEvent) => void

const listeners = new Set<Listener>()
let connection: HubConnection | null = null
let retry: ReturnType<typeof setTimeout> | undefined

/** One shared connection while any screen listens; the session cookie authenticates it. */
function connect() {
  if (connection) return
  const hub = new HubConnectionBuilder().withUrl(new URL('/hubs/chat', window.location.origin).href).withAutomaticReconnect().configureLogging(LogLevel.None).build()
  hub.on('message', (event: ChatEvent) => listeners.forEach((l) => l({ kind: 'message', event })))
  hub.on('notification', (n: { type: string }) => listeners.forEach((l) => l({ kind: 'notification', type: n.type })))
  hub.onclose(scheduleRetry)
  connection = hub
  start()
}

function start() {
  connection?.start().catch(scheduleRetry)
}

/** Automatic reconnect covers dropped connections; this covers a first start that failed (offline, deploy). */
function scheduleRetry() {
  if (retry || listeners.size === 0) return
  retry = setTimeout(() => {
    retry = undefined
    if (connection?.state === HubConnectionState.Disconnected) start()
  }, 15_000)
}

function subscribe(listener: Listener): () => void {
  listeners.add(listener)
  connect()
  return () => {
    listeners.delete(listener)
    if (listeners.size > 0) return
    clearTimeout(retry)
    retry = undefined
    void connection?.stop()
    connection = null
  }
}

/** Calls `handler` for everything pushed to the signed-in user. */
export function useHubEvents(handler: Listener, enabled = true) {
  const latest = useRef(handler)
  useEffect(() => {
    latest.current = handler
  })
  useEffect(() => (enabled ? subscribe((e) => latest.current(e)) : undefined), [enabled])
}

/** Calls `handler` for every chat message pushed to the signed-in user. */
export function useChatEvents(handler: (event: ChatEvent) => void, enabled = true) {
  useHubEvents((e) => {
    if (e.kind === 'message') handler(e.event)
  }, enabled)
}

/** Calls `handler` when a new in-app notification arrives. */
export function useNotificationEvents(handler: (type: string) => void, enabled = true) {
  useHubEvents((e) => {
    if (e.kind === 'notification') handler(e.type)
  }, enabled)
}
