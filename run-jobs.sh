#!/usr/bin/env bash
#
# ------------------------------------------------------------------------------
# This script runs the many jobs in the provided directory either using
# [GNU Parallel](https://www.gnu.org/software/parallel) or the cluster's API, if any.
#
# Usage:
# run-jobs.sh
#   --jobs_dir_path <full path>
#   [--max_number_batches <maximum number of batches (where one batch is composed by many jobs), 32 by default>]
#   [--memory <amount of RAM per job in MegaBytes, 1024 by default (only used on Clusters)>]
#   [help]
# ------------------------------------------------------------------------------ Utils

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" > /dev/null 2>&1 && pwd)"

#
# Get number of CPUs
#
_get_number_of_cpus() {
  local USAGE="Usage: ${FUNCNAME[0]}"
  if [ "$#" != 0 ] ; then
    echo "$USAGE" >&2
    return 1
  fi

  num_cpus="1" # by default
  dist=$(uname)
  if [ "$dist" == "Darwin" ]; then
    num_cpus=$(sysctl -n hw.ncpu)
  elif [ "$dist" == "Linux" ]; then
    num_cpus=$(cat /proc/cpuinfo | grep 'cpu cores' | sort -u | cut -f2 -d':' | cut -f2 -d' ')
  fi

  echo "$num_cpus"
  return 0
}

#
# Check whether the machine has the software that allows one to run jobs
# in parallel.  Return true ('0') if yes, otherwise it returns false ('1').
#
_can_I_run_jobs_simultaneously() {
  if man parallel > /dev/null 2>&1; then
    return 0 # true
  fi
  return 1 # false
}

# ----------------------------------------------------------------- Requirements

# Check whether the machine has the software that allows one to run jobs in parallel
_can_I_run_jobs_simultaneously || die "[ERROR] Scripts are optimized to run on a machine with [GNU Parallel](https://www.gnu.org/software/parallel). Please make sure it is the case."

# ------------------------------------------------------------------------- Args

USAGE="Usage: ${BASH_SOURCE[0]} \
  [--jobs_dir_path <full path to jobs directory, $SCRIPT_DIR/jobs by default>] \
  [--max_number_batches <maximum number of batches (where one batch is composed by many jobs), 32 by default>] \
  [--memory <amount of RAM per job in MegaBytes, 1024 by default (only used on Clusters)>] \
  [help]"
if [ "$#" -ne "0" ] && [ "$#" -ne "1" ] && [ "$#" -ne "2" ] && [ "$#" -ne "4" ] && [ "$#" -ne "6" ] && [ "$#" -ne "8" ]; then
  die "$USAGE"
fi

JOBS_DIR_PATH="$SCRIPT_DIR/jobs"
SECONDS_PER_JOB="360"
MAX_NUMBER_BATCHES="32" # A batch is composed by one or more jobs
MEMORY="1024" # In MegaBytes

while [[ "$1" = --* ]]; do
  OPTION=$1; shift
  case $OPTION in
    (--jobs_dir_path)
      JOBS_DIR_PATH=$1;
      shift;;
    (--seconds_per_job)
      SECONDS_PER_JOB=$1;
      shift;;
    (--max_number_batches)
      MAX_NUMBER_BATCHES=$1;
      shift;;
    (--memory)
      MEMORY=$1;
      shift;;
    (--help)
      echo "$USAGE";
      exit 0;;
    (*)
      die "$USAGE";;
  esac
done

# Check whether all arguments have been initialized
[ "$JOBS_DIR_PATH" != "" ]      || die "[ERROR] Missing --jobs_dir_path argument!"
[ "$SECONDS_PER_JOB" != "" ]    || die "[ERROR] Missing --seconds_per_job argument!"
[ "$MAX_NUMBER_BATCHES" != "" ] || die "[ERROR] Missing --max_number_batches argument!"
[ "$MEMORY" != "" ]             || die "[ERROR] Missing --memory argument!"

# Check whether required directories/files do exist
[ -d "$JOBS_DIR_PATH" ]         || die "[ERROR] $JOBS_DIR_PATH does not exist!"

# ------------------------------------------------------------------------- Main

# Remove any previously generated batch file/job
find "$JOBS_DIR_PATH" -mindepth 1 -maxdepth 1 -type f -name "batch-*.sh*" -exec rm -f {} \;
find "$JOBS_DIR_PATH" -mindepth 1 -maxdepth 1 -type f -name "batch-*.txt" -exec rm -f {} \;

# How many jobs have not been completed successfully?
number_of_jobs_to_run=0
while read -r script_file_path; do
  log_file_path=$(echo "$script_file_path" | sed 's|job.sh$|job.log|g')
  if [ -s "$log_file_path" ]; then # Log exists and it is not empty
    if ! tail -n1 "$log_file_path" | grep -q "^DONE\!$"; then
      number_of_jobs_to_run=$((number_of_jobs_to_run+1))
    fi
  else
    number_of_jobs_to_run=$((number_of_jobs_to_run+1))
  fi
done < <(find "$JOBS_DIR_PATH" -type f -name "job.sh")
echo "[DEBUG] number of jobs to run: $number_of_jobs_to_run"

# Prepare jobs for running
all_jobs_file_path="$JOBS_DIR_PATH/all-jobs.txt"
rm -f "$all_jobs_file_path"

while read -r script_file_path; do
  # Has this job been completed successfully?
  log_file_path=$(echo "$script_file_path" | sed 's|job.sh$|job.log|g')
  if [ -s "$log_file_path" ]; then # Log exists and it is not empty
    if tail -n1 "$log_file_path" | grep -q "^DONE\!$"; then
      continue
    else
      # In case of re-run of a unsuccessfully execution, clean up the log file
      rm -f "$log_file_path"; touch "$log_file_path"
    fi
  else
    touch "$log_file_path"
  fi

  timefactor=$(grep "^# timefactor:" "$script_file_path" | cut -f2 -d':')
  script_seconds_per_job=$(echo "$SECONDS_PER_JOB * $timefactor" | bc)
  echo "bash \"$script_file_path\"" >> "$all_jobs_file_path"
done < <(find "$JOBS_DIR_PATH" -type f -name "job.sh" | shuf)

# Run jobs
number_of_cpus=$(_get_number_of_cpus)
echo "[DEBUG] Running all jobs with GNU Parallel using $number_of_cpus CPUs"
parallel --progress -j "$number_of_cpus" -a "$all_jobs_file_path"


echo "DONE!"
exit 0

# EOF