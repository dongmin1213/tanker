#!/usr/bin/env python3
"""ASC API로 Tanker App Store 배포 프로파일 생성·설치.

같은 이름의 기존 프로파일을 지우고 새로 만든다 (멱등 재생성 — 프로파일은 API로 언제든 재발급 가능).
필요: pip3 install pyjwt cryptography. 경로/키는 환경변수로 재정의 가능.
  ASC_KEY_ID, ASC_ISSUER, ASC_P8, TANKER_BUNDLE
"""
import base64, json, os, sys, time, urllib.error, urllib.parse, urllib.request

import jwt

KEY_ID = os.environ.get("ASC_KEY_ID", "27H547L8YJ")
ISSUER = os.environ.get("ASC_ISSUER", "8bc44c53-8575-42be-8b25-b1cca4483963")
P8 = os.environ.get("ASC_P8", os.path.expanduser("~/.appstoreconnect/private_keys/AuthKey_27H547L8YJ.p8"))
BUNDLE = os.environ.get("TANKER_BUNDLE", "com.dongmin1213.tanker")
PROFILE_NAME = "Tanker AppStore"
API = "https://api.appstoreconnect.apple.com"


def make_token():
    with open(P8) as f:
        key = f.read()
    return jwt.encode(
        {"iss": ISSUER, "iat": int(time.time()) - 30, "exp": int(time.time()) + 1200, "aud": "appstoreconnect-v1"},
        key, algorithm="ES256", headers={"kid": KEY_ID, "typ": "JWT"})


def call(token, method, path, body=None):
    req = urllib.request.Request(API + path, method=method)
    req.add_header("Authorization", "Bearer " + token)
    data = None
    if body is not None:
        req.add_header("Content-Type", "application/json")
        data = json.dumps(body).encode()
    try:
        with urllib.request.urlopen(req, data) as r:
            raw = r.read()
            return json.loads(raw) if raw else {}
    except urllib.error.HTTPError as e:
        print(f"HTTP {e.code} {path}\n{e.read().decode()[:800]}", file=sys.stderr)
        raise


def main():
    token = make_token()

    b = call(token, "GET", f"/v1/bundleIds?filter[identifier]={BUNDLE}")
    bundles = [d for d in b["data"] if d["attributes"]["identifier"] == BUNDLE]
    if not bundles:
        sys.exit(f"번들 ID 리소스 없음: {BUNDLE}")
    bundle_rid = bundles[0]["id"]
    print("bundleId 리소스:", bundle_rid)

    c = call(token, "GET", "/v1/certificates?filter[certificateType]=DISTRIBUTION&limit=20")
    certs = c["data"]
    if not certs:
        sys.exit("배포 인증서 없음")
    for d in certs:
        print("cert:", d["id"], d["attributes"]["name"], d["attributes"]["expirationDate"])
    cert_ids = [d["id"] for d in certs]

    # 동명 프로파일 재생성 (멱등)
    p = call(token, "GET", "/v1/profiles?filter[name]=" + urllib.parse.quote(PROFILE_NAME))
    for d in p.get("data", []):
        call(token, "DELETE", f"/v1/profiles/{d['id']}")
        print("기존 프로파일 삭제:", d["id"])

    new = call(token, "POST", "/v1/profiles", {
        "data": {
            "type": "profiles",
            "attributes": {"name": PROFILE_NAME, "profileType": "IOS_APP_STORE"},
            "relationships": {
                "bundleId": {"data": {"type": "bundleIds", "id": bundle_rid}},
                "certificates": {"data": [{"type": "certificates", "id": i} for i in cert_ids]},
            },
        }
    })
    content = new["data"]["attributes"]["profileContent"]
    uuid = new["data"]["attributes"]["uuid"]
    out_dir = os.path.expanduser("~/Library/Developer/Xcode/UserData/Provisioning Profiles")
    os.makedirs(out_dir, exist_ok=True)
    out = os.path.join(out_dir, f"{uuid}.mobileprovision")
    with open(out, "wb") as f:
        f.write(base64.b64decode(content))
    print("프로파일 설치:", out)
    print("PROFILE_NAME=" + PROFILE_NAME)


if __name__ == "__main__":
    main()
