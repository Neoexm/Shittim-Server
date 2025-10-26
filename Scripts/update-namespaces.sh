#!/bin/bash

set -e

DRY_RUN=false
EXCLUDE_PATTERNS="plana,Plana,packages,bin,obj,node_modules,.git"

print_help() {
    cat << EOF
Schale Namespace Migration Tool

USAGE:
    ./update-namespaces.sh [OPTIONS]

OPTIONS:
    --dry-run           Show changes without modifying files
    --exclude PATTERNS  Additional patterns to exclude (comma-separated)
    --help              Show this help message

EXAMPLES:
    ./update-namespaces.sh --dry-run
    ./update-namespaces.sh
    ./update-namespaces.sh --exclude "Legacy.cs,Old.cs"

EOF
}

while [[ $# -gt 0 ]]; do
    case $1 in
        --dry-run)
            DRY_RUN=true
            shift
            ;;
        --exclude)
            EXCLUDE_PATTERNS="$EXCLUDE_PATTERNS,$2"
            shift 2
            ;;
        --help)
            print_help
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            print_help
            exit 1
            ;;
    esac
done

should_exclude() {
    local file_path="$1"
    IFS=',' read -ra PATTERNS <<< "$EXCLUDE_PATTERNS"
    for pattern in "${PATTERNS[@]}"; do
        if [[ "$file_path" == *"$pattern"* ]]; then
            return 0
        fi
    done
    return 1
}

echo -e "\033[36mSchale Namespace Migration Tool\033[0m"
echo -e "\033[36m================================\033[0m"
echo ""

if [ "$DRY_RUN" = true ]; then
    echo -e "\033[33mDRY RUN MODE - No files will be modified\033[0m"
    echo ""
fi

modified_count=0
scanned_count=0

while IFS= read -r -d '' file; do
    if should_exclude "$file"; then
        continue
    fi
    
    ((scanned_count++))
    
    original_content=$(<"$file")
    modified_content="$original_content"
    
    modified_content=$(echo "$modified_content" | sed -E 's/\bPlana\./Schale./g')
    modified_content=$(echo "$modified_content" | sed -E 's/\bPlana([A-Za-z0-9_]+)/Schale\1/g')
    
    if [ "$original_content" != "$modified_content" ]; then
        echo -e "\033[33mModified: $file\033[0m"
        ((modified_count++))
        
        if [ "$DRY_RUN" = false ]; then
            echo "$modified_content" > "$file"
            echo -e "  \033[32mApplied changes\033[0m"
        else
            echo -e "  \033[36mPreview: Changes detected\033[0m"
        fi
    fi
done < <(find . -name "*.cs" -type f -print0)

echo ""
echo -e "\033[36mSummary:\033[0m"
echo "  Files scanned: $scanned_count"
if [ $modified_count -gt 0 ]; then
    echo -e "  Files modified: \033[33m$modified_count\033[0m"
else
    echo -e "  Files modified: \033[32m$modified_count\033[0m"
fi

if [ "$DRY_RUN" = true ] && [ $modified_count -gt 0 ]; then
    echo ""
    echo -e "\033[33mRun without --dry-run to apply changes\033[0m"
fi

if [ "$DRY_RUN" = false ] && [ $modified_count -gt 0 ]; then
    echo ""
    echo -e "\033[32mChanges applied successfully!\033[0m"
fi