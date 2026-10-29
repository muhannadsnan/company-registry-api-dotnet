type PaginatorProps = {
    page: number
    totalPages: number
    onPageChange: (page: number) => void
}

function Paginator({ page, totalPages, onPageChange }: PaginatorProps) {
    return (
        <nav>
            <button disabled={page <= 1} onClick={() => onPageChange(page - 1)}>Previous</button>
            <span>Page {page} of {totalPages}</span>
            <button disabled={page >= totalPages} onClick={() => onPageChange(page + 1)}>Next</button>
        </nav>
    )
}
export default Paginator