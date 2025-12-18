import os, sys, time, json, argparse
import requests

def make_poster(base_url):
    def _post(path, body):
        url = f"{base_url}{path}"
        r = requests.post(url, json=body, timeout=10)
        r.raise_for_status()
        return r.json()
    return _post

def create_order(post, order_id, bins, color="COLCYAN", interval_ms=0):
    lines = [{"binId": b, "qty": 1, "color": color} for b in bins]
    resp = post("/orders", {"orderId": order_id, "lines": lines, "mode": "GUIDE"})
    print("Created:", resp)
    if interval_ms:
        time.sleep(interval_ms/1000)

def main():
    default_central = os.getenv("CENTRAL_URL", "http://central:8080")
    parser = argparse.ArgumentParser(description="ERP Order Simulator")
    parser.add_argument("--central", default=default_central, help="Central base URL")
    parser.add_argument("--prefix", default="SO-20251016-", help="Order ID prefix")
    parser.add_argument("--start", type=int, default=1, help="Start number")
    parser.add_argument("--end", type=int, default=3, help="End number inclusive")
    parser.add_argument("--bins", default="A-01-01,A-01-02", help="Comma-separated binIds")
    parser.add_argument("--color", default="COLCYAN", help="LED color")
    parser.add_argument("--interval", type=int, default=0, help="ms between orders")
    args = parser.parse_args()

    post = make_poster(args.central)
    bins = [b.strip() for b in args.bins.split(",") if b.strip()]
    for i in range(args.start, args.end + 1):
        oid = f"{args.prefix}{i:03d}"
        create_order(post, oid, bins, color=args.color, interval_ms=args.interval)

if __name__ == "__main__":
    main()
