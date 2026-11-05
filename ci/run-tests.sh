#!/bin/bash
set -uo pipefail

usage() {
  cat <<'EOF'
Usage: ci/run-tests.sh <lane> [--results DIR] [--area AREA] [--only PROJECT]...

Lanes:
  build  builds every project in Nethereum.slnx except those in ci/skip-build.txt
         (MAUI, Unity and .NET Framework 3.5 / 4.8 projects that need macOS, the Unity
         Editor or Windows targeting packs)
  unit   every test that needs no fetched external: excludes Requires=Externals and every opt-in category
  full   everything except the opt-in categories that need live networks or very large local data
  <Category>  only the tests carrying [Trait("Category", "<Category>")], e.g. Zisk, MainnetFork,
              BundlerSpec, LivePeer, LiveNetwork, LargeFixture, ExternalNode, Performance

--area runs only the projects of one area in ci/areas.txt (evm, eest, corechain, appchain, devp2p, aa, zk,
or rest for every project no other area matches).

Projects listed in ci/skip-projects.txt are not run. Results land in DIR (default .testresults/<lane>);
the previous run's summary is kept as summary.previous.txt and compared.
EOF
}

[ $# -ge 1 ] || { usage; exit 2; }
LANE=$1; shift
ROOT=$(cd "$(dirname "$0")/.." && pwd)
RESULTS="$ROOT/.testresults/$LANE"
ONLY=()
AREA=""
while [ $# -gt 0 ]; do
  case "$1" in
    --results) RESULTS=$2; shift 2 ;;
    --only) ONLY+=("$2"); shift 2 ;;
    --area) AREA=$2; shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "unknown argument $1"; usage; exit 2 ;;
  esac
done

OPT_IN="Category!=Zisk&Category!=MainnetFork&Category!=BundlerSpec&Category!=LivePeer&Category!=LiveNetwork&Category!=LargeFixture&Category!=ExternalNode&Category!=LegacyFork-Debug&Category!=LegacyFork-DebugSubtest&Category!=MainnetBlockCell-Trace&Category!=GasDiagnostic&Category!=Performance"
case "$LANE" in
  build) FILTER="" ;;
  unit) FILTER="Requires!=Externals&$OPT_IN" ;;
  full) FILTER="$OPT_IN" ;;
  Zisk) FILTER="(Category=Zisk|Category=ZiskVerifier)&Category!=LiveNetwork" ;;
  *) FILTER="Category=$LANE" ;;
esac

cd "$ROOT"

area_of() {
  local area prefixes prefix
  while read -r area prefixes; do
    for prefix in $prefixes; do
      [[ $1 == "$prefix"* ]] && { echo "$area"; return; }
    done
  done < <(grep -vE '^(#|\s*$)' ci/areas.txt)
  echo rest
}
if [ -n "$AREA" ] && [ "$AREA" != rest ] && ! grep -qE "^$AREA\s" ci/areas.txt; then
  echo "unknown area $AREA (see ci/areas.txt)"; exit 2
fi
mkdir -p "$RESULTS/trx"
[ -f "$RESULTS/summary.txt" ] && mv "$RESULTS/summary.txt" "$RESULTS/summary.previous.txt"

for native in Nethereum.Signer.Bls.Herumi Nethereum.CircomWitnessCalc Nethereum.ZkProofs.RapidSnark; do
  dotnet pack "src/$native/$native.csproj" -c Release -o nativeartifacts > "$RESULTS/prepack.$native.log" 2>&1 \
    || { echo "PREPACK FAILED $native (see $RESULTS/prepack.$native.log)"; exit 1; }
done

if [ "$LANE" = "build" ]; then
  echo "START $(date -Is) lane=build commit=$(git rev-parse --short HEAD)" > "$RESULTS/summary.txt"
  dotnet run ci/run-tests-tools.cs -- build-filter "$RESULTS/build.slnf"
  dotnet build "$RESULTS/build.slnf" -c Release -nologo -clp:ErrorsOnly > "$RESULTS/build.log" 2>&1 < /dev/null
  rc=$?
  grep -oE "error [A-Z]+[0-9]+: .*" "$RESULTS/build.log" | sort -u | cut -c1-240 | sed 's/^/ERROR /' >> "$RESULTS/summary.txt"
  echo "$rc build" >> "$RESULTS/summary.txt"
  echo "DONE $(date -Is)" >> "$RESULTS/summary.txt"
  exit $rc
fi

grep -o 'Path="tests[^"]*\.csproj"' Nethereum.slnx | sed 's/^Path="//; s/"$//' | sort -u > "$RESULTS/projects.txt"
echo "START $(date -Is) lane=$LANE area=${AREA:-all} commit=$(git rev-parse --short HEAD) filter=$FILTER" > "$RESULTS/summary.txt"

while read -r project; do
  name=$(basename "$project" .csproj)
  if grep -qx "$name" ci/skip-projects.txt; then echo "SKIP $name (ci/skip-projects.txt)" >> "$RESULTS/summary.txt"; continue; fi
  if [ -n "$AREA" ] && [ "$(area_of "$name")" != "$AREA" ]; then continue; fi
  if [ ${#ONLY[@]} -gt 0 ] && ! printf '%s\n' "${ONLY[@]}" | grep -qx "$name"; then continue; fi
  tfms=$(grep -o '<TargetFrameworks\?>[^<]*' "$project" | head -1 | sed 's/.*>//' | tr ';' ' ')
  for tfm in $tfms; do
    case "$tfm" in
      net6.0|net8.0|net9.0|net10.0) roll="" ;;
      netcoreapp3.1|net5.0) roll="Major" ;;
      *) echo "SKIP $name $tfm (not runnable on this platform)" >> "$RESULTS/summary.txt"; continue ;;
    esac
    parts=("") tags=("")
    split=$(grep -E "^$name[[:space:]]" ci/project-splits.txt | head -1)
    if [ -n "$split" ]; then
      read -r _ trait values <<< "$split"
      parts=() tags=() rest=""
      for value in $values; do parts+=("$trait=$value"); tags+=(".$value"); rest="$rest&$trait!=$value"; done
      parts+=("${rest#&}"); tags+=(".rest")
    fi
    for i in "${!parts[@]}"; do
      filter="$FILTER"
      [ -n "${parts[$i]}" ] && filter="${FILTER:+$FILTER&}${parts[$i]}"
      run="$name.$tfm${tags[$i]}"
      log="$RESULTS/$run.log"
      DOTNET_ROLL_FORWARD=$roll timeout 3600 dotnet test "$project" -c Release -f "$tfm" --filter "$filter" \
        --blame-hang-timeout 40m --blame-hang-dump-type none --logger "trx;LogFileName=$run.trx" --results-directory "$RESULTS/trx" > "$log" 2>&1 < /dev/null
      rc=$?
      line=$(grep -E "^(Passed|Failed)!|error CS[0-9]+|Build FAILED|No test is available|No test matches" "$log" | tail -1 | cut -c1-200)
      [ $rc -eq 124 ] && line="TIMEOUT after 3600s"
      grep -qi "test run was aborted" "$log" && line="ABORTED (test host stopped; counts are partial) $line"
      echo "$rc $name $tfm${tags[$i]} $line" >> "$RESULTS/summary.txt"
    done
  done
done < "$RESULTS/projects.txt"

if [ -d "$ROOT/.testresults/conformance/diagnosis" ]; then
  mkdir -p "$RESULTS/diagnosis" && cp "$ROOT/.testresults/conformance/diagnosis/"* "$RESULTS/diagnosis/" 2>/dev/null
fi

echo "DONE $(date -Is)" >> "$RESULTS/summary.txt"

if [ -f "$RESULTS/summary.previous.txt" ]; then
  dotnet run ci/run-tests-tools.cs -- summary-diff "$RESULTS/summary.previous.txt" "$RESULTS/summary.txt"
fi

grep -vE "^(START|DONE|SKIP|0 )" "$RESULTS/summary.txt" | grep -q . && exit 1 || exit 0
