import assert from 'node:assert/strict';
import test from 'node:test';
import {
	formatEnhanceUnexpectedError,
	isHtmlLikeResponse,
	parseSafeJsonResponse,
	resolveHttpFailure
} from '../src/lib/utils/http.ts';

const t = (key) => key;

test('isHtmlLikeResponse detects HTML payloads and content types', () => {
	assert.equal(isHtmlLikeResponse('<!DOCTYPE html><html></html>', 'text/plain'), true);
	assert.equal(isHtmlLikeResponse('  \n<html><head><title>413</title></head></html>'), true);
	assert.equal(isHtmlLikeResponse('{"ok":true}', 'text/html; charset=utf-8'), true);
	assert.equal(isHtmlLikeResponse('{"ok":true}', 'application/json'), false);
});

test('parseSafeJsonResponse handles Nginx 413 HTML without throwing SyntaxError', async () => {
	const response = new Response(
		'<html><head><title>413 Request Entity Too Large</title></head><body><h1>413 Request Entity Too Large</h1></body></html>',
		{ status: 413, headers: { 'content-type': 'text/html' } }
	);
	const result = await parseSafeJsonResponse(response, t);
	assert.equal(result.ok, false);
	if (!result.ok) {
		assert.equal(result.status, 413);
		assert.equal(result.errorKey, 'common.payloadTooLarge');
		assert.equal(result.message, 'common.payloadTooLarge');
	}
});

test('parseSafeJsonResponse detects login redirect HTML as sessionExpired', async () => {
	const response = {
		status: 200,
		ok: true,
		redirected: true,
		url: 'https://app.example.com/login?redirectTo=%2Fapp%2Ftickets%2F1%2Fattachments',
		headers: new Headers({ 'content-type': 'text/html; charset=utf-8' }),
		text: async () => '<!doctype html><html><body>Login</body></html>'
	};

	const result = await parseSafeJsonResponse(response, t);
	assert.equal(result.ok, false);
	if (!result.ok) {
		assert.equal(result.status, 401);
		assert.equal(result.errorKey, 'common.sessionExpired');
		assert.equal(result.redirectedToLogin, true);
	}
});

test('parseSafeJsonResponse parses valid JSON and extracts server error message', async () => {
	const okResponse = new Response(JSON.stringify({ ok: true, id: 42 }), {
		status: 200,
		headers: { 'content-type': 'application/json' }
	});
	const okResult = await parseSafeJsonResponse(okResponse, t);
	assert.equal(okResult.ok, true);
	if (okResult.ok) {
		assert.equal(okResult.data.id, 42);
	}

	const errResponse = new Response(JSON.stringify({ message: 'Photo upload failed' }), {
		status: 400,
		headers: { 'content-type': 'application/json' }
	});
	const errResult = await parseSafeJsonResponse(errResponse, t);
	assert.equal(errResult.ok, false);
	if (!errResult.ok) {
		assert.equal(errResult.status, 400);
		assert.equal(errResult.message, 'Photo upload failed');
	}
});

test('resolveHttpFailure maps gateway errors cleanly', () => {
	const res = resolveHttpFailure({ status: 504, rawText: '<html>504 Gateway Time-out</html>', contentType: 'text/html' }, t);
	assert.equal(res.errorKey, 'common.serverUnavailable');
});

test('formatEnhanceUnexpectedError converts Safari JSON HTML parse errors into friendly message', () => {
	const safariErr = new SyntaxError("JSON Parse error: Unrecognized token '<'");
	assert.equal(formatEnhanceUnexpectedError(safariErr, t), 'common.invalidServerResponse');

	const chromeErr = new SyntaxError('Unexpected token \'<\', "<!DOCTYPE "... is not valid JSON');
	assert.equal(formatEnhanceUnexpectedError(chromeErr, t), 'common.invalidServerResponse');
});
