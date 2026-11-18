import { redirect, type RequestHandler } from '@sveltejs/kit';

export const POST: RequestHandler = ({ cookies }) => {
	cookies.delete('servora_session', { path: '/' });
	throw redirect(303, '/login');
};
