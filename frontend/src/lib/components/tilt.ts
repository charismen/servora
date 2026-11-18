/**
 * Svelte custom action to make an element tilt in 3D on hover.
 * ponytail: Native platform CSS/DOM implementation, zero dependencies.
 */
export function tilt(node: HTMLElement, options = { maxTilt: 8, scale: 1.025 }) {
	const handleMouseMove = (e: MouseEvent) => {
		const rect = node.getBoundingClientRect();
		const x = e.clientX - rect.left;
		const y = e.clientY - rect.top;
		const xc = rect.width / 2;
		const yc = rect.height / 2;
		const dx = x - xc;
		const dy = y - yc;

		const tiltX = -(dy / yc) * options.maxTilt;
		const tiltY = (dx / xc) * options.maxTilt;

		node.style.transform = `perspective(1000px) rotateX(${tiltX.toFixed(2)}deg) rotateY(${tiltY.toFixed(2)}deg) scale3d(${options.scale}, ${options.scale}, ${options.scale})`;
		node.style.boxShadow = '0 32px 64px rgba(0, 0, 0, 0.24), 0 16px 32px rgba(0, 0, 0, 0.12)';
		node.style.zIndex = '10';
	};

	const handleMouseLeave = () => {
		node.style.transform = 'perspective(1000px) rotateX(0deg) rotateY(0deg) scale3d(1, 1, 1)';
		node.style.boxShadow = '';
		node.style.zIndex = '';
	};

	node.addEventListener('mousemove', handleMouseMove);
	node.addEventListener('mouseleave', handleMouseLeave);

	node.style.transition = 'transform 0.15s ease-out, box-shadow 0.25s ease, border-color 0.25s ease';

	return {
		destroy() {
			node.removeEventListener('mousemove', handleMouseMove);
			node.removeEventListener('mouseleave', handleMouseLeave);
		}
	};
}
