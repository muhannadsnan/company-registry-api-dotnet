type Props = {
    page: number
    pageSize: number
    totalCount: number
    onPageChange: (page: number) => void
}

export default function Paginator({ page, pageSize, totalCount, onPageChange }: Props) {
    const totalPages = Math.max(1, Math.ceil(totalCount / pageSize))

    const button =
        'rounded border border-gray-700 px-3 py-2 bg-black hover:bg-gray-900 disabled:opacity-40 disabled:hover:bg-transparent'

    return (
        <nav className="mt-4 flex items-center justify-center gap-4 text-sm">
            <button onClick={() => onPageChange(page - 1)} disabled={page <= 1} className={button}>
                Prev
            </button>
            <span className="text-gray-400">
                Page {page} of {totalPages}
            </span>
            <button onClick={() => onPageChange(page + 1)} disabled={page >= totalPages} className={button}>
                Next
            </button>
        </nav>
    )
}
