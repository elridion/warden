#!/usr/bin/env bash
# Uploads a Thunderstore-format package zip to Hexium and submits it.
#
# Usage: HEXIUM_TOKEN=... publish-hexium.sh <package.zip>
# Optional env: HEXIUM_AUTHOR (team namespace), HEXIUM_CATEGORIES (JSON array of category names),
# HEXIUM_INSTALL_LOCATION (client, server, both or client_opt_server), HEXIUM_API (API base URL).
#
# The flow follows Hexium's OpenAPI spec (https://hexium.gg/api/openapi.json): initiate a multipart
# upload, PUT each part to its presigned URL, finish the upload with the parts' ETags, submit the
# upload for the Valheim community, then set the listing's install location and categories.
# Submitting does not apply either: a new listing defaults to the "Client-only" install location.

set -Eeuo pipefail

zip="${1:?usage: publish-hexium.sh <package.zip>}"
: "${HEXIUM_TOKEN:?HEXIUM_TOKEN is not set}"
author="${HEXIUM_AUTHOR:-el_team}"
# "Client & Server" and friends are not categories to send: Hexium derives them from the install location.
categories="${HEXIUM_CATEGORIES:-[\"Building\", \"Pieces\", \"Valheim 1.0\", \"Open Source\"]}"
# Every client and the server need the mod (NetworkCompatibility EveryoneMustHaveMod).
install_location="${HEXIUM_INSTALL_LOCATION:-both}"
api="${HEXIUM_API:-https://hexium.gg/api/experimental}"
api="${api%/}"
site="${api%/api/experimental}"
name=$(unzip -p "$zip" manifest.json | jq -r '.name')

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

post() {
  curl -sS --fail-with-body -X POST "${3:-$api}$1" \
    -H "Authorization: Bearer $HEXIUM_TOKEN" \
    -H "Content-Type: application/json" \
    --data "$2"
}

size=$(stat -c %s "$zip")
echo "Initiating upload of $(basename "$zip") ($size bytes)"
post /usermedia/initiate-upload/ \
  "$(jq -n --arg f "$(basename "$zip")" --argjson s "$size" '{filename: $f, file_size_bytes: $s}')" \
  > "$work/initiate.json"
uuid=$(jq -r '.user_media.uuid' "$work/initiate.json")

abort() {
  echo "::error::Upload failed, aborting upload $uuid"
  post "/usermedia/$uuid/abort-upload/" "$(jq -n --arg u "$uuid" '{uuid: $u}')" > /dev/null || true
  exit 1
}
trap 'abort' ERR

echo '[]' > "$work/parts.json"
while read -r part; do
  url=$(jq -r '.url' <<< "$part")
  offset=$(jq -r '.offset' <<< "$part")
  length=$(jq -r '.length' <<< "$part")
  number=$(jq -r '.part_number' <<< "$part")
  tail -c +"$((offset + 1))" "$zip" | head -c "$length" > "$work/part"
  etag=$(curl -sS --fail-with-body -X PUT "$url" \
      -H "Content-Type: application/octet-stream" \
      --data-binary "@$work/part" -D - -o /dev/null \
    | tr -d '\r' | sed -n 's/^[Ee][Tt][Aa][Gg]: *//p')
  [ -n "$etag" ] || { echo "::error::Part $number returned no ETag"; false; }
  jq --arg e "$etag" --argjson n "$number" '. + [{ETag: $e, PartNumber: $n}]' "$work/parts.json" > "$work/parts.tmp"
  mv "$work/parts.tmp" "$work/parts.json"
  echo "Uploaded part $number ($length bytes)"
done < <(jq -c '.upload_urls[]' "$work/initiate.json")

post "/usermedia/$uuid/finish-upload/" "$(jq '{parts: .}' "$work/parts.json")" > /dev/null
trap - ERR
echo "Finished upload $uuid"

body=$(jq -n --arg a "$author" --arg u "$uuid" --argjson c "$categories" '{
  author_name: $a,
  communities: ["valheim"],
  community_categories: {valheim: $c},
  categories: [],
  has_nsfw_content: false,
  upload_uuid: $u
}')
post /submission/submit/ "$body" > "$work/submit.json"

hidden=$(jq -r '.hidden // false' "$work/submit.json")
echo "Submitted to Hexium as $author (hidden: $hidden)"
if [ "$hidden" = "true" ]; then
  echo "::notice::Hexium accepted the package but reports it as hidden (it may still be scanning)."
fi

metadata=$(jq -n --arg l "$install_location" --argjson c "$categories" '{install_location: $l, categories: $c}')
post "/api/mod/$author/$name/set-metadata" "$metadata" "$site" > "$work/metadata.json"
echo "Set $author-$name metadata: $(jq -c '{install_location, categories}' "$work/metadata.json")"
