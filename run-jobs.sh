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
  if sbatch --version > /dev/null 2>&1; then
    return 0 # true
  fi
  if man parallel > /dev/null 2>&1; then
    return 0 # true
  fi
  return 1 # false
}

# ----------------------------------------------------------------- Requirements

# Check whether the machine has the software that allows one to run jobs in parallel
_can_I_run_jobs_simultaneously || die "[ERROR] Scripts are optimized to run on clusters with a SGE system or a machine with [GNU Parallel](https://www.gnu.org/software/parallel). Please make sure it is the case."

#
# Generate a header for a batch based on the host's name.
#
_generate_batch_header() {
  local USAGE="Usage: ${FUNCNAME[0]} <batch script file patch> <batch timeout in seconds> <amount of RAM in MB>"
  if [ "$#" != "3" ]; then
    echo "$USAGE" >&2
    return 1
  fi

  # Args
  local batch_script_file_path="$1"
  local batch_timeout_in_seconds="$2"
  local memory="$3"

  # Local vars
  local host_name=$(hostname)
  local timeout=$(printf '%02d:%02d:%02d\n' $((batch_timeout_in_seconds/3600)) $((batch_timeout_in_seconds%3600/60)) $((batch_timeout_in_seconds%60)))

  # hash-bang
  echo "#!/usr/bin/env bash"

  if [ "$host_name" == "submit.grid.up.pt" ]; then # e.g., submit.grid.up.pt at FE/UP
    echo "#SBATCH --job-name=$(basename "$batch_script_file_path")"
    echo "#SBATCH --output=$batch_script_file_path.out"
    echo "#SBATCH --error=$batch_script_file_path.err"
    echo "#SBATCH --nodes=1 # ensure that all cores are on one machine"
    # sbatch does not launch tasks, it requests an allocation of resources and
    # submits a batch script.  This option advises the Slurm controller that job
    # steps run within the allocation will launch a maximum of number tasks
    # and to provide for sufficient resources.  The default is one task per
    # node, but note that the --cpus-per-task option will change this default.
    echo "#SBATCH --ntasks=1"
    # Request that `ntasks` be invoked on each node.  If used with the --ntasks
    # option, the --ntasks option will take precedence and the --ntasks-per-node
    # will be treated as a maximum count of tasks per node.  Meant to be used
    # with the --nodes option.  This is related to --cpus-per-task=ncpus, but
    # does not require knowledge of the actual number of cpus on each node.
    # In some cases, it is more convenient to be able to request that no more
    # than a specific number of tasks be invoked on each node.
    echo "#SBATCH --ntasks-per-node=1"
    # Advise the Slurm controller that ensuing job steps will require `ncpus`
    # number of processors per task.  Without this option, the controller will
    # just try to allocate one processor per task.
    # For instance, consider an application that has 4 tasks, each requiring 3
    # processors.  If our cluster is comprised of quad-processors nodes and we
    # simply ask for 12 processors, the controller might give us only 3 nodes.
    # However, by using the --cpus-per-task=3 options, the controller knows that
    # each task requires 3 processors on the same node, and the controller will
    # grant an allocation of 4 nodes, one for each of the 4 tasks.
    echo "#SBATCH --cpus-per-task=1"
    # Minimum memory required per allocated CPU.  Default units are megabytes.
    echo "#SBATCH --mem-per-cpu=$memory"
    # Set a minimum time limit on the job allocation.
    echo "#SBATCH --time=$timeout"
    # Set partition
    echo "#SBATCH --partition=batch"
    # Unload any module
    echo "module purge"
  fi

  echo ""
  return 0
}

#
# Run a batch script using host's infrastructure.
#
_run_batch_script() {
  local USAGE="Usage: ${FUNCNAME[0]} <batch script file patch>"
  if [ "$#" != "1" ]; then
    echo "$USAGE" >&2
    return 1
  fi

  local batch_script_file_path="$1"
  [ -s "$batch_script_file_path" ] || die "[ERROR] $batch_script_file_path does not exist or it is empty!"

  echo "[DEBUG] Running $batch_script_file_path ..."

  local host_name=$(hostname)
  if [ "$host_name" == "submit.grid.up.pt" ]; then
    sbatch "$batch_script_file_path"
  else
    bash "$batch_script_file_path"
  fi

  return 0
}

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

host_name=$(hostname)

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

if [ "$host_name" == "submit.grid.up.pt" ]; then # Cluster path (sbatch)

  # Number of batches that could be executed in parallel, given machine's limits
  number_of_jobs_per_batch=$(echo "($number_of_jobs_to_run + $MAX_NUMBER_BATCHES - 1) / $MAX_NUMBER_BATCHES" | bc)
  echo "[DEBUG] number of jobs per batch: $number_of_jobs_per_batch"

  # Create batches
  batch_id=1
  batch_jobs_file_path="$JOBS_DIR_PATH/batch-$batch_id.txt"; rm -f "$batch_jobs_file_path"
  count_number_jobs_in_batch=0
  echo "[DEBUG] Creating batch-$batch_id"

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
    echo "timeout --signal=SIGTERM ${script_seconds_per_job}s bash \"$script_file_path\"" >> "$batch_jobs_file_path"

    count_number_jobs_in_batch=$((count_number_jobs_in_batch+1))
    if [ "$count_number_jobs_in_batch" -ge "$number_of_jobs_per_batch" ]; then
      count_number_jobs_in_batch=0 # Reset counter of jobs in a batch
      batch_id=$((batch_id+1)) # Increment batch counter
      batch_jobs_file_path="$JOBS_DIR_PATH/batch-$batch_id.txt"; rm -f "$batch_jobs_file_path"
      echo "[DEBUG] Creating batch-$batch_id"
    fi
  done < <(find "$JOBS_DIR_PATH" -type f -name "job.sh" | shuf)

  # How many?
  number_of_batches_to_run=$(find "$JOBS_DIR_PATH" -mindepth 1 -maxdepth 1 -type f -name "batch-*.txt" | wc -l)
  echo "[DEBUG] number of batches to run: $number_of_batches_to_run"

  # Create and submit batch scripts
  for batch_id in $(seq 1 $number_of_batches_to_run); do
    echo "[DEBUG] Creating job-$batch_id"

    batch_jobs_file_path="$JOBS_DIR_PATH/batch-$batch_id.txt"
    [ -s "$batch_jobs_file_path" ] || die "[ERROR] $batch_jobs_file_path does not exist or it is empty!"
    batch_script_file_path="$JOBS_DIR_PATH/batch-$batch_id.sh"
    rm -f "$batch_script_file_path"

    # How much time would a batch require to run all jobs
    batch_total_time_in_seconds=$(grep "^timeout --signal=SIGTERM " "$batch_jobs_file_path" | cut -f3 -d' ' | tr -d 's' | paste -sd+ - | bc)
    if [ "$batch_total_time_in_seconds" -gt "432000" ]; then # > 5 days
      # Jobs in the HPC at FE/UP can only run for 5 days
      batch_total_time_in_seconds="432000"
    fi

    # Init batch file
    _generate_batch_header \
      "$batch_script_file_path" \
      "$batch_total_time_in_seconds" \
      "$MEMORY" > "$batch_script_file_path" || die "[ERROR] Failed to init batch file $batch_script_file_path!"

    cat "$batch_jobs_file_path" >> "$batch_script_file_path"

    echo ""               >> "$batch_script_file_path"
    echo "echo \"DONE!\"" >> "$batch_script_file_path"
    echo "exit 0"         >> "$batch_script_file_path"
    echo ""               >> "$batch_script_file_path"
    echo "# EOF"          >> "$batch_script_file_path"
  done

  # Submit batches
  while read -r batch_script_file_path; do
    _run_batch_script "$batch_script_file_path" || die "[ERROR] Failed to run $batch_script_file_path!"
  done < <(find "$JOBS_DIR_PATH" -mindepth 1 -maxdepth 1 -type f -name "batch-*.sh" | shuf)

else # Local path (GNU Parallel)

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

  number_of_cpus=$(_get_number_of_cpus)
  echo "[DEBUG] Running all jobs with GNU Parallel using $number_of_cpus CPUs"
  parallel --progress -j "$number_of_cpus" -a "$all_jobs_file_path"

fi

echo "DONE!"
exit 0

# EOF
