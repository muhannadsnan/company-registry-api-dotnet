// Shared Tailwind class combinations: what an element IS. The page adds where it SITS (margins, flex-1).
// Full class names only, so Tailwind's scanner finds them.
export const TW = {
    btn: {
        primary: 'rounded bg-blue-600 px-4 py-2 font-medium text-white hover:bg-blue-500 disabled:opacity-50',
        secondary:
            'rounded border border-gray-700 bg-gray-900 px-3 py-1 hover:bg-black disabled:opacity-40 disabled:hover:bg-gray-900',
    },
    input: 'rounded border border-gray-700 bg-gray-950 px-3 py-2 focus:border-blue-500 focus:outline-none',
    link: 'text-blue-400 hover:opacity-80',
    error: 'text-red-400',
    muted: 'text-gray-400',
}
