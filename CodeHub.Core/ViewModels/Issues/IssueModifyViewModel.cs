using System;
using System.Threading.Tasks;
using ReactiveUI;
using System.Reactive;
using CodeHub.Core.Factories;
using System.Reactive.Linq;
using System.Collections.Generic;
using CodeHub.Core.Services;
using System.Collections.ObjectModel;
using System.Linq;
using Octokit;

namespace CodeHub.Core.ViewModels.Issues
{
    public abstract class IssueModifyViewModel : BaseViewModel, ILoadableViewModel
    {
        private string _subject;
        public string Subject
        {
            get { return _subject; }
            set { this.RaiseAndSetIfChanged(ref _subject, value); }
        }

		private string _content;
		public string Content
		{
			get { return _content; }
            set { this.RaiseAndSetIfChanged(ref _content, value); }
		}

        private User _assignedUser;
        public User AssignedUser
        {
            get { return _assignedUser; }
            protected set { this.RaiseAndSetIfChanged(ref _assignedUser, value); }
        }

        private Milestone _assignedMilestone;
        public Milestone AssignedMilestone
        {
            get { return _assignedMilestone; }
            protected set { this.RaiseAndSetIfChanged(ref _assignedMilestone, value); }
        }

        private IReadOnlyList<Label> _assignedLabels;
        public IReadOnlyList<Label> AssignedLabels 
        {
            get { return _assignedLabels; }
            protected set { this.RaiseAndSetIfChanged(ref _assignedLabels, value); }
        }

        private bool? _isCollaborator;
        public bool? IsCollaborator
        {
            get { return _isCollaborator; }
            private set { this.RaiseAndSetIfChanged(ref _isCollaborator, value); }
        }

        public IssueAssigneeViewModel Assignees { get; private set; }

        public IssueLabelsViewModel Labels { get; private set; }

        public IssueMilestonesViewModel Milestones { get; private set; }

        public IReactiveCommand<object> GoToAssigneesCommand { get; private set; }

        public IReactiveCommand<object> GoToMilestonesCommand { get; private set; }

        public IReactiveCommand<object> GoToLabelsCommand { get; private set; }

        public IReactiveCommand<Unit> LoadCommand { get; private set; }

		public string RepositoryOwner { get; set; }

		public string RepositoryName { get; set; }

        public IReactiveCommand<Issue> SaveCommand { get; private set; }

        public IReactiveCommand<bool> DismissCommand { get; private set; }

        protected IssueModifyViewModel(
            ISessionService applicationService, 
            IAlertDialogFactory alertDialogFactory)
	    {
            GoToAssigneesCommand = ReactiveCommand.Create();
            GoToLabelsCommand = ReactiveCommand.Create();
            GoToMilestonesCommand = ReactiveCommand.Create();
            AssignedLabels = new ReactiveList<Label>();

            // Make sure repeated access is cached with Lazy
            var getAssignees = new Lazy<Task<IReadOnlyList<User>>>(() => applicationService.GitHubClient.Issue.Assignee.GetAllForRepository(RepositoryOwner, RepositoryName));
            var getMilestones = new Lazy<Task<IReadOnlyList<Milestone>>>(() => applicationService.GitHubClient.Issue.Milestone.GetAllForRepository(RepositoryOwner, RepositoryName));
            var getLables = new Lazy<Task<IReadOnlyList<Label>>>(() => applicationService.GitHubClient.Issue.Labels.GetAllForRepository(RepositoryOwner, RepositoryName));

            Assignees = new IssueAssigneeViewModel(
                () => getAssignees.Value,
                () => Task.FromResult(AssignedUser),
                x => Task.FromResult(AssignedUser = x));

            Milestones = new IssueMilestonesViewModel(
                () => getMilestones.Value,
                () => Task.FromResult(AssignedMilestone),
                x => Task.FromResult(AssignedMilestone = x));

            Labels = new IssueLabelsViewModel(
                () => getLables.Value,
                () => Task.FromResult(new ReadOnlyCollection<Label>(AssignedLabels.ToList()) as IReadOnlyList<Label>),
                x => {
                    AssignedLabels = x;
                    return Task.FromResult(0);
                });

            Labels.SelectedLabels.Changed
                .Select(_ => new ReadOnlyCollection<Label>(Labels.SelectedLabels.ToList()))
                .Subscribe(x => AssignedLabels = x);


            var canSave = this.WhenAnyValue(x => x.Subject).Select(x => !string.IsNullOrEmpty(x));
            SaveCommand = ReactiveCommand.CreateAsyncTask(canSave, _ => {
                using (alertDialogFactory.Activate("Saving..."))
                    return Save();
            });

            // This is because the stupid ReactiveUI issue with the tableview :(
            SaveCommand.Subscribe(_ => Dismiss());

            LoadCommand = ReactiveCommand.CreateAsyncTask(async t =>
            {
                try
                {
                    IsCollaborator = await applicationService.GitHubClient.Repository.RepoCollaborators
                        .IsCollaborator(RepositoryOwner, RepositoryName, applicationService.Account.Username);
                }
                catch
                {
                    IsCollaborator = false;
                }
            });

            DismissCommand = ReactiveCommand.CreateAsyncTask(t => Discard());
            DismissCommand.Where(x => x).Subscribe(_ => Dismiss());
	    }

        protected abstract Task<Issue> Save();

        protected abstract Task<bool> Discard();
    }
}

