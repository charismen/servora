import { browser } from '$app/environment';
import { writable } from 'svelte/store';

const storageKey = 'service-management.auth';

export type AuthUser = {
	id: string;
	fullName: string;
	role: string;
};

export type AuthState = {
	token: string | null;
	user: AuthUser | null;
};

const initialState: AuthState = browser
	? JSON.parse(localStorage.getItem(storageKey) ?? '{"token":null,"user":null}')
	: { token: null, user: null };

function createAuthStore() {
	const { subscribe, set } = writable<AuthState>(initialState);

	return {
		subscribe,
		setSession: (state: AuthState) => {
			if (browser) {
				localStorage.setItem(storageKey, JSON.stringify(state));
			}

			set(state);
		},
		clear: () => {
			if (browser) {
				localStorage.removeItem(storageKey);
			}

			set({ token: null, user: null });
		}
	};
}

export const auth = createAuthStore();
