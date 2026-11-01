export type Session = {
    accessToken: string
    expiresAt: string
    username: string
    scopes: string[]
}

const storageKey = 'session'

export function loadSession(): Session | null {
    const json = localStorage.getItem(storageKey)
    if (!json) {
        return null
    }
    const session: Session = JSON.parse(json)
    if (new Date(session.expiresAt).getTime() <= Date.now()) {
        localStorage.removeItem(storageKey)
        return null
    }
    return session
}

export function saveSession(session: Session) {
    localStorage.setItem(storageKey, JSON.stringify(session))
}

export function clearSession() {
    localStorage.removeItem(storageKey)
}
