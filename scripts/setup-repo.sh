#!/bin/bash

# Setup Script for ScholarshipMatcher GitHub Repository
# This script initializes git, creates .gitignore, and prepares for first commit

set -e

# Color codes
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Function to print colored output
print_info() {
    echo -e "${CYAN}$1${NC}"
}

print_success() {
    echo -e "${GREEN}$1${NC}"
}

print_warning() {
    echo -e "${YELLOW}$1${NC}"
}

print_error() {
    echo -e "${RED}$1${NC}"
}

GITHUB_USERNAME=$1
REPOSITORY_NAME=${2:-"ScholarshipMatcher"}

print_info "🚀 ScholarshipMatcher Repository Setup"
echo ""

# Check if git is installed
if ! command -v git &> /dev/null; then
    print_error "❌ Git is not installed. Please install Git first."
    print_warning "Download from: https://git-scm.com/downloads"
    exit 1
fi

print_success "✓ Git is installed"

# Navigate to project root
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
cd "$PROJECT_ROOT"

print_warning "Project directory: $PROJECT_ROOT"
echo ""

# Check if .git already exists
if [ -d ".git" ]; then
    print_warning "⚠ Git repository already initialized"
    read -p "Do you want to continue? (y/n) " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        print_warning "Exiting..."
        exit 0
    fi
else
    # Initialize git repository
    print_info "Initializing git repository..."
    git init
    print_success "✓ Git repository initialized"
fi

# Configure git user if not set
GIT_USER_NAME=$(git config user.name || true)
if [ -z "$GIT_USER_NAME" ]; then
    read -p "Enter your Git user name: " USER_NAME
    git config user.name "$USER_NAME"
    print_success "✓ Git user name set"
fi

GIT_USER_EMAIL=$(git config user.email || true)
if [ -z "$GIT_USER_EMAIL" ]; then
    read -p "Enter your Git email: " USER_EMAIL
    git config user.email "$USER_EMAIL"
    print_success "✓ Git email set"
fi

echo ""

# Create initial commit
print_info "Preparing initial commit..."

# Stage all files
print_warning "Staging files..."
git add .

# Check if there are changes to commit
if [ -z "$(git status --porcelain)" ]; then
    print_warning "⚠ No changes to commit"
else
    # Create commit
    print_warning "Creating initial commit..."
    git commit -m "Initial commit: ScholarshipMatcher application with Azure deployment"
    print_success "✓ Initial commit created"
fi

# Rename branch to main if needed
CURRENT_BRANCH=$(git branch --show-current)
if [ "$CURRENT_BRANCH" != "main" ]; then
    print_warning "Renaming branch to 'main'..."
    git branch -M main
    print_success "✓ Branch renamed to main"
fi

echo ""

# GitHub repository setup
if [ -z "$GITHUB_USERNAME" ]; then
    print_info "📝 Next Steps:"
    echo ""
    echo "1. Create a new repository on GitHub:"
    echo "   https://github.com/new"
    echo ""
    echo "2. Add the remote origin:"
    echo "   git remote add origin https://github.com/YOUR_USERNAME/$REPOSITORY_NAME.git"
    echo ""
    echo "3. Push to GitHub:"
    echo "   git push -u origin main"
    echo ""
else
    REMOTE_URL="https://github.com/$GITHUB_USERNAME/$REPOSITORY_NAME.git"

    # Check if remote already exists
    EXISTING_REMOTE=$(git remote get-url origin 2>/dev/null || true)
    if [ -n "$EXISTING_REMOTE" ]; then
        print_warning "⚠ Remote 'origin' already exists: $EXISTING_REMOTE"
        read -p "Do you want to update it to $REMOTE_URL? (y/n) " -n 1 -r
        echo
        if [[ $REPLY =~ ^[Yy]$ ]]; then
            git remote set-url origin "$REMOTE_URL"
            print_success "✓ Remote updated"
        fi
    else
        print_warning "Adding remote origin..."
        git remote add origin "$REMOTE_URL"
        print_success "✓ Remote added: $REMOTE_URL"
    fi

    echo ""
    print_info "📝 Next Steps:"
    echo ""
    echo "1. Make sure you've created the repository on GitHub:"
    echo "   https://github.com/new"
    echo ""
    echo "2. Push to GitHub:"
    echo "   git push -u origin main"
    echo ""
fi

echo "4. After pushing, follow the deployment guide:"
echo "   See docs/DEPLOYMENT.md"
echo ""

# Check if GitHub CLI is installed
if command -v gh &> /dev/null; then
    print_info "💡 Tip: You have GitHub CLI installed!"
    print_warning "You can create and push the repository with one command:"
    echo "   gh repo create $REPOSITORY_NAME --public --source=. --push"
    echo ""
fi

print_success "🎉 Repository setup complete!"
