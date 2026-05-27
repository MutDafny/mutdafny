#!/usr/bin/env bash
#
# ------------------------------------------------------------------------------
# This script generates a dataset of mutants of a given dataset of Dafny programs. 
# By default, one mutation at a time is applied per program, following the principles 
# of mutation testing. We also provide an option to apply more than one mutation at 
# a time, which can be useful for a variety of use cases, e.g., for building a dataset 
# of multi-fault programs for APR.
#
# Usage:
# run.sh
#   <full path to the folder with the base dataset, e.g., $SCRIPT_DIR/../DafnyBench/DafnyBench/dataset/ground_truth/> 
#   [--recursive <whether to recursively include dafny files in sub-directories of the dataset root, e.g., false (by default)>]
#   [--subjects_whitelist <optional file that indicates the programs in the dataset that are allowed to be mutated, e.g., none (by default)>]
#   [--method <the specific method of the program to mutate, e.g., Main (none by default)>]
#   [--range <the specific range of positions of the program to mutate, e.g., 150-300 (none by default)>]
#   [--num_mutations <the number of mutations to apply to the input program, e.g., 1 (by default)>]
#   [help]
# ------------------------------------------------------------------------------ General utils

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" > /dev/null 2>&1 && pwd)"

die() {
  echo "$@" >&2
  exit 1
}

# ------------------------------------------------------------------------------ Args

USAGE="Usage: ${BASH_SOURCE[0]}
   <full path to the folder with the base dataset, e.g., $SCRIPT_DIR/../DafnyBench/DafnyBench/dataset/ground_truth> 
   [--recursive (recursively include dafny files in sub-directories of the dataset root)]
   [--subjects_whitelist <optional file that indicates the programs in the dataset that are allowed to be mutated, e.g., none (by default)>]
   [--method <the specific method of the program to mutate, e.g., Main (none by default)>]
   [--range <the specific range of positions of the program to mutate, e.g., 150-300 (none by default)>]
   [--num_mutations <the number of mutations to apply to the input program, e.g., 1 (by default)>]
   [help]"
if [ "$#" -ne "1" ] && [ "$#" -ne "3" ] && [ "$#" -ne "5" ] && [ "$#" -ne "7" ] && [ "$#" -ne "9" ] && [ "$#" -ne "11" ]; then
  die "$USAGE"
fi

if [ "$#" -eq "1" ] && [ "$1" = "--help" ]; then
    echo "$USAGE"
    exit 0
fi

INPUT_DATASET_DIR=$1
shift
RECURSIVE="false"
WHITELIST_FILE=""
METHOD=""
RANGE=""
NUM_MUTS=1
while [[ "$1" = --* ]]; do
  OPTION=$1; shift
  case $OPTION in
    (--recursive)
      RECURSIVE=$1;
      shift;;
    (--subjects_whitelist)
      WHITELIST_FILE=$1;
      shift;;
    (--method)
      METHOD=$1;
      shift;;
    (--range)
      RANGE=$1;
      shift;;
    (--num_mutations)
      NUM_MUTS=$1;
      shift;;
    (--help)
      echo "$USAGE";
      exit 0;;
    (*)
      die "$USAGE";;
  esac
done

# ------------------------------------------------------------------------- Main

# Create jobs' directories
jobs_dir_path="$SCRIPT_DIR/jobs"
master_job_script_file_path="$SCRIPT_DIR/run.sh"
[ -s "$master_job_script_file_path" ] || die "[ERROR] $master_job_script_file_path does not exist or it is empty!"
mkdir -p "$jobs_dir_path"

dataset_files=""
if [ "$RECURSIVE" = "true" ]; then
  dataset_files=$(find "$INPUT_DATASET_DIR" -type f -name "*.dfy")
else
  dataset_files="$INPUT_DATASET_DIR"/*.dfy
fi

# Create set of jobs
for program_file in $dataset_files; do
  if [ -f "$WHITELIST_FILE" ] && ! grep -q "^$(basename "$program_file" .dfy)$" "$WHITELIST_FILE"; then
    continue
  fi

  echo "[DEBUG] $program_file"
  program_name=$(basename "$program_file" .dfy)

  job_script_dir_path="$jobs_dir_path/$program_name"
  job_script_file_path="$job_script_dir_path/job.sh"
  job_log_file_path="$job_script_dir_path/job.log"
  mkdir -p "$job_script_dir_path"
  rm -f "$job_log_file_path"
  touch "$job_script_file_path" "$job_log_file_path"

  echo "#!/usr/bin/env bash" > "$job_script_file_path"
  echo "#"                  >> "$job_script_file_path"
  echo "# timefactor:1"     >> "$job_script_file_path"
  echo "bash $master_job_script_file_path \
    \"$program_file\" \
    --method $METHOD \
    --range $RANGE \
    --num_mutations $NUM_MUTS \
    --run_dir \"$job_script_dir_path\" \
    --output_dir \"$SCRIPT_DIR/mutants/$NUM_MUTS-mut\" > \"$job_log_file_path\" 2>&1" >> "$job_script_file_path"
done

echo "Jobs have been created. Please run the $SCRIPT_DIR/run-jobs.sh script on the generated jobs, e.g., $SCRIPT_DIR/run-jobs.sh --jobs_dir_path $jobs_dir_path."

echo "DONE!"
exit 0