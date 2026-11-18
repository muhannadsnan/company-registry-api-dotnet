import { TW } from '../tw'

type Props = {
    page: number
    pageSize: number
    totalCount: number
    onPageChange: (page: number) => void
}

export default function Paginator({ page, pageSize, totalCount, onPageChange }: Props) {
    const totalPages = Math.max(1, Math.ceil(totalCount / pageSize))

    return (
        <nav className="mt-4 flex items-center justify-center gap-4 text-sm">
            <button onClick={() => onPageChange(page - 1)} disabled={page <= 1} className={TW.btn.secondary}>
                Prev
            </button>
            <span className="text-gray-400">
                Page {page} of {totalPages}
            </span>
            <button onClick={() => onPageChange(page + 1)} disabled={page >= totalPages} className={TW.btn.secondary}>
                Next
            </button>
        </nav>
    )
}
