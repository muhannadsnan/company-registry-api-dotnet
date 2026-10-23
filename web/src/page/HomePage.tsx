import { Link } from "react-router";

export default function HomePage() {
    return (
        <main>
            <>
                <nav>
                    <Link to="/">Home</Link>
                    <Link to="/companies">Search</Link>
                    <Link to="/about">About</Link>
                </nav>
                
                <h1>Home Page</h1>

                <h3>Router works. Go to search</h3>
            </>
        </main>
    )
}