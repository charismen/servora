/**
 * Svelte custom action to make an element magnetic (attracts to cursor on hover).
 * ponytail: Native platform CSS/JS implementation, zero dependencies.
 */
export function magnetic(node: HTMLElement, options = { range: 50, strength: 0.3 }) {
	const handleMouseMove = (e: MouseEvent) => {
		const rect = node.getBoundingClientRect();
		// Compute center of the element
		const xc = rect.left + rect.width / 2;
		const yc = rect.top + rect.height / 2;
		// Cursor distance from center
		const dx = e.clientX - xc;
		const dy = e.clientY - yc;
		const dist = Math.hypot(dx, dy);

		if (dist < options.range) {
			const pullX = dx * options.strength;
			const pullY = dy * options.strength;
			node.style.transform = `translate3d(${pullX.toFixed(1)}px, ${pullY.toFixed(1)}px, 0) scale3d(1.02, 1.02, 1.02)`;
		} else {
			node.style.transform = 'translate3d(0px, 0px, 0px) scale3d(1, 1, 1)';
		}
	};

	const handleMouseLeave = () => {
		node.style.transform = 'translate3d(0px, 0px, 0px) scale3d(1, 1, 1)';
	};

	window.addEventListener('mousemove', handleMouseMove);
	node.addEventListener('mouseleave', handleMouseLeave);

	node.style.transition = 'transform 0.15s cubic-bezier(0.25, 1, 0.5, 1)';

	return {
		destroy() {
			window.removeEventListener('mousemove', handleMouseMove);
			node.removeEventListener('mouseleave', handleMouseLeave);
		}
	};
}
