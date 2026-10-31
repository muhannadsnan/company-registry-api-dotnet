type PaginatorProps = {
    page: number
    totalPages: number
    onPageChange: (page: number) => void
}

function Paginator({ page, totalPages, onPageChange }: PaginatorProps) {
    return (
        <nav className="paginator">
            <button disabled={page <= 1} onClick={() => onPageChange(page - 1)} className="prev">
                Previous
            </button>
            <span>
                Page {page} of {totalPages}
            </span>
            <button disabled={page >= totalPages} onClick={() => onPageChange(page + 1)} className="next">
                Next
            </button>
        </nav>
    )
}
export default Paginator
