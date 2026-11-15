type Props = {
    page: number
    pageSize: number
    totalCount: number
    onPageChange: (page: number) => void
}

export default function Paginator({ page, pageSize, totalCount, onPageChange }: Props) {
    const totalPages = Math.max(1, Math.ceil(totalCount / pageSize))

    return (
        <nav className="paginator">
            <button onClick={() => onPageChange(page - 1)} disabled={page <= 1}>
                Prev
            </button>

            <span>
                Page {page} of {totalPages}
            </span>

            <button onClick={() => onPageChange(page + 1)} disabled={page >= totalPages}>
                Next
            </button>
        </nav>
    )
}
