import os
import re
import psycopg2
import subprocess

def discover_connection_string():
    paths = [
        "src/MURO.API/appsettings.json",
        "appsettings.json",
        "../src/MURO.API/appsettings.json"
    ]
    for p in paths:
        if os.path.exists(p):
            try:
                with open(p, "r", encoding="utf-8") as f:
                    content = f.read()
                    match = re.search(r'"DefaultConnection"\s*:\s*"([^"]+)"', content)
                    if match:
                        conn_str = match.group(1)
                        params = {}
                        for item in conn_str.split(';'):
                            if '=' in item:
                                k, v = item.split('=', 1)
                                k_lower = k.strip().lower()
                                v_val = v.strip()
                                if k_lower == 'host': params['host'] = v_val
                                elif k_lower == 'port': params['port'] = int(v_val)
                                elif k_lower == 'database': params['dbname'] = v_val
                                elif k_lower == 'username': params['user'] = v_val
                                elif k_lower == 'password': params['password'] = v_val
                        if params:
                            return params, p
            except Exception:
                pass
    return None, None

def get_postgres_ip():
    try:
        res = subprocess.check_output(
            ["docker", "inspect", "-f", "{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}", "muro_mng_postgres"],
            text=True
        )
        ip = res.strip()
        if ip:
            return ip
    except Exception:
        pass
    return "localhost"

def main():
    params, _ = discover_connection_string()
    if not params:
        params = {
            'host': 'localhost',
            'port': 5432,
            'dbname': 'muro_demo',
            'user': 'muro_user',
            'password': 'MuroDem0_2026!Str0ng'
        }
    
    ip = get_postgres_ip()
    if ip and ip != "localhost":
        params['host'] = ip
        
    print(f"Connecting to database {params.get('dbname')} on {params.get('host')}...")
    try:
        conn = psycopg2.connect(**params)
        cur = conn.cursor()
        
        cur.execute('SELECT "Title", "CreatedAt" FROM "Courses" WHERE "IsDeleted" = False ORDER BY "Title";')
        courses = cur.fetchall()
        
        print("\n" + "=" * 50)
        print(f"      COURSES IN DATABASE (Total: {len(courses)})")
        print("=" * 50)
        for idx, (title, created) in enumerate(courses, 1):
            print(f" {idx:3d}. {title}")
        print("=" * 50)
        
        cur.close()
        conn.close()
    except Exception as e:
        print(f"Database error: {e}")

if __name__ == '__main__':
    main()
