import assert from 'node:assert/strict';
import test from 'node:test';
import { isSupportedImageFile, normalizeImageFile, resolveSupportedImageMime } from '../src/lib/utils/image.ts';

test('resolveSupportedImageMime accepts standard image MIMEs and normalizes image/jpg', () => {
	assert.equal(resolveSupportedImageMime({ type: 'image/jpeg', name: 'a.jpg' }), 'image/jpeg');
	assert.equal(resolveSupportedImageMime({ type: 'image/jpg', name: 'a.jpg' }), 'image/jpeg');
	assert.equal(resolveSupportedImageMime({ type: 'image/png', name: 'a.png' }), 'image/png');
	assert.equal(resolveSupportedImageMime({ type: 'image/webp', name: 'a.webp' }), 'image/webp');
	assert.equal(resolveSupportedImageMime({ type: 'IMAGE/JPEG; charset=utf-8', name: 'a.jpg' }), 'image/jpeg');
});

test('resolveSupportedImageMime falls back to filename extension when browser omits MIME type', () => {
	assert.equal(resolveSupportedImageMime({ type: '', name: 'photo.JPEG' }), 'image/jpeg');
	assert.equal(resolveSupportedImageMime({ type: 'application/octet-stream', name: 'shot.png' }), 'image/png');
	assert.equal(resolveSupportedImageMime({ type: '', name: 'diagram.webp' }), 'image/webp');
	assert.equal(resolveSupportedImageMime({ type: 'text/plain', name: 'notes.txt' }), null);
	assert.equal(isSupportedImageFile({ type: '', name: 'receipt.jpg' }), true);
	assert.equal(isSupportedImageFile({ type: 'application/pdf', name: 'doc.pdf' }), false);
});

test('normalizeImageFile wraps image/jpg into image/jpeg File', () => {
	const jpgFile = new File([new Uint8Array([1, 2, 3])], 'intake.jpg', { type: 'image/jpg' });
	const normalized = normalizeImageFile(jpgFile);
	assert.equal(normalized.type, 'image/jpeg');
	assert.equal(normalized.name, 'intake.jpg');
});
