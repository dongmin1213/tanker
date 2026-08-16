#!/usr/bin/env python3
"""ASC API로 Tanker App Store 배포 프로파일 생성·다운로드."""
import base64, json, time, sys, urllib.request, urllib.error
import jwt

KEY_ID = "27H547L8YJ"
ISSUER = "8bc44c53-8575-42be-8b25-b1cca4483963"
P8 = "/Users/ddong8/.appstoreconnect/private_keys/AuthKey_27H547L8YJ.p8"
BUNDLE = "com.dongmin1213.tanker"
API = "https://api.appstoreconnect.apple.com"

with open(P8) as f:
    key = f.read()
token = jwt.encode(
    {"iss": ISSUER, "iat": int(time.time()) - 30, "exp": int(time.time()) + 1200, "aud": "appstoreconnect-v1"},
    key, algorithm="ES256", headers={"kid": KEY_ID, "typ": "JWT"})

def call(method, path, body=None):
    req = urllib.request.Request(API + path, method=method)
    req.add_header("Authorization", "Bearer " + token)
    data = None
    if body is not None:
        req.add_header("Content-Type", "application/json")
        data = json.dumps(body).encode()
    try:
        with urllib.request.urlopen(req, data) as r:
            return json.loads(r.read())
    except urllib.error.HTTPError as e:
        print(f"HTTP {e.code} {path}\n{e.read().decode()[:800]}", file=sys.stderr)
        raise

# 1) 번들 ID 리소스
b = call("GET", f"/v1/bundleIds?filter[identifier]={BUNDLE}")
bundles = [d for d in b["data"] if d["attributes"]["identifier"] == BUNDLE]
if not bundles:
    sys.exit("번들 ID 리소스 없음")
bundle_rid = bundles[0]["id"]
print("bundleId 리소스:", bundle_rid)

# 2) 유효한 배포 인증서
c = call("GET", "/v1/certificates?filter[certificateType]=DISTRIBUTION&limit=20")
certs = c["data"]
if not certs:
    sys.exit("배포 인증서 없음")
for d in certs:
    print("cert:", d["id"], d["attributes"]["name"], d["attributes"]["expirationDate"])
cert_ids = [d["id"] for d in certs]

# 3) 기존 동명 프로파일 정리 후 생성
p = call("GET", "/v1/profiles?filter[name]=Tanker%20AppStore")
for d in p.get("data", []):
    call("DELETE", f"/v1/profiles/{d['id']}")
    print("기존 프로파일 삭제:", d["id"])

new = call("POST", "/v1/profiles", {
    "data": {
        "type": "profiles",
        "attributes": {"name": "Tanker AppStore", "profileType": "IOS_APP_STORE"},
        "relationships": {
            "bundleId": {"data": {"type": "bundleIds", "id": bundle_rid}},
            "certificates": {"data": [{"type": "certificates", "id": i} for i in cert_ids]},
        },
    }
})
content = new["data"]["attributes"]["profileContent"]
uuid = new["data"]["attributes"]["uuid"]
out = f"/Users/ddong8/Library/Developer/Xcode/UserData/Provisioning Profiles/{uuid}.mobileprovision"
with open(out, "wb") as f:
    f.write(base64.b64decode(content))
print("프로파일 설치:", out)
print("PROFILE_NAME=Tanker AppStore")
