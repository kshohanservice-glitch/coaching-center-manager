using System.Collections.ObjectModel;
using ShikkhaDesk.Application.Security;
using ShikkhaDesk.Application.Services;
using ShikkhaDesk.Core.Entities;
using ShikkhaDesk.Core.Services;
using ShikkhaDesk.Wpf.Infrastructure;

namespace ShikkhaDesk.Wpf.ViewModels;

public sealed class UserRow
{
    public Guid Id { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string LastLogin { get; init; } = string.Empty;
}

public sealed class PermissionItem
{
    public string Code { get; init; } = string.Empty;
    public string Group { get; init; } = string.Empty;
    public string NameBn { get; init; } = string.Empty;

    private bool _isGranted;
    public bool IsGranted
    {
        get => _isGranted;
        set => _isGranted = value;
    }

    public override string ToString() => $"{Group} · {NameBn}";
}

/// <summary>
/// Users, roles and permissions. Permissions are enforced in the service layer; this screen only
/// decides what each role is allowed to do.
/// </summary>
public sealed class UsersViewModel : ViewModelBase
{
    private readonly IUserService _users;
    private readonly IDialogService _dialogs;
    private readonly AppState _state;

    public UsersViewModel(IUserService users, IDialogService dialogs, AppState state)
    {
        _users = users;
        _dialogs = dialogs;
        _state = state;

        CreateUserCommand = new AsyncRelayCommand(_ => CreateUserAsync());
        ToggleActiveCommand = new AsyncRelayCommand(_ => ToggleActiveAsync(), () => SelectedUser is not null);
        ResetPasswordCommand = new AsyncRelayCommand(_ => ResetPasswordAsync(), () => SelectedUser is not null);
        CreateRoleCommand = new AsyncRelayCommand(_ => CreateRoleAsync());
        LoadPermissionsCommand = new AsyncRelayCommand(_ => LoadPermissionsAsync(), () => SelectedRole is not null);
        SavePermissionsCommand = new AsyncRelayCommand(_ => SavePermissionsAsync(), () => SelectedRole is not null);

        _ = LoadAsync();
    }

    public ObservableCollection<UserRow> Users { get; } = [];
    public ObservableCollection<Role> Roles { get; } = [];
    public ObservableCollection<PermissionItem> Permissions { get; } = [];

    public string NewUserName { get; set; } = string.Empty;
    public string NewFullName { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string NewRoleName { get; set; } = string.Empty;

    private UserRow? _selectedUser;
    public UserRow? SelectedUser
    {
        get => _selectedUser;
        set
        {
            if (SetField(ref _selectedUser, value))
            {
                ((AsyncRelayCommand)ToggleActiveCommand).RaiseCanExecuteChanged();
                ((AsyncRelayCommand)ResetPasswordCommand).RaiseCanExecuteChanged();
            }
        }
    }

    private Role? _selectedRole;
    public Role? SelectedRole
    {
        get => _selectedRole;
        set
        {
            if (SetField(ref _selectedRole, value))
            {
                ((AsyncRelayCommand)LoadPermissionsCommand).RaiseCanExecuteChanged();
                ((AsyncRelayCommand)SavePermissionsCommand).RaiseCanExecuteChanged();
                if (value is not null)
                {
                    _ = LoadPermissionsAsync();
                }
            }
        }
    }

    public ICommand CreateUserCommand { get; }
    public ICommand ToggleActiveCommand { get; }
    public ICommand ResetPasswordCommand { get; }
    public ICommand CreateRoleCommand { get; }
    public ICommand LoadPermissionsCommand { get; }
    public ICommand SavePermissionsCommand { get; }

    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            IReadOnlyList<Role> roles = await _users.GetRolesAsync();

            Users.Clear();
            foreach (User user in await _users.GetUsersAsync())
            {
                Users.Add(new UserRow
                {
                    Id = user.Id,
                    UserName = user.UserName,
                    FullName = user.FullName,
                    Role = roles.FirstOrDefault(r => r.Id == user.RoleId)?.Name ?? "—",
                    Status = user.IsActive ? "সক্রিয়" : "নিষ্ক্রিয়",
                    LastLogin = user.LastLoginAtUtc is null
                        ? "কখনো নয়"
                        : Core.Digits.Format(user.LastLoginAtUtc.Value, _state.UseBengaliDigits)
                });
            }

            Roles.Clear();
            foreach (Role role in roles)
            {
                Roles.Add(role);
            }

            SelectedRole ??= Roles.FirstOrDefault();
        }, "তালিকা আনা হচ্ছে…");
    }

    private async Task CreateUserAsync()
    {
        await RunAsync(async () =>
        {
            if (NewUserName.Trim().Length < 3 || NewFullName.Trim().Length == 0 || NewPassword.Length == 0)
            {
                throw new ValidationException("ব্যবহারকারীর নাম, পুরো নাম ও পাসওয়ার্ড দিন।");
            }

            await _users.CreateUserAsync(NewUserName.Trim(), NewFullName.Trim(), NewPassword,
                SelectedRole?.Id);

            NewUserName = NewFullName = NewPassword = string.Empty;
            OnPropertyChanged(nameof(NewUserName));
            OnPropertyChanged(nameof(NewFullName));
            OnPropertyChanged(nameof(NewPassword));
            await LoadAsync();
            StatusMessage = "ব্যবহারকারী তৈরি করা হয়েছে।";
        }, "তৈরি করা হচ্ছে…");
    }

    private async Task ToggleActiveAsync()
    {
        if (SelectedUser is null)
        {
            return;
        }

        bool deactivate = SelectedUser.Status == "সক্রিয়";
        await RunAsync(async () =>
        {
            await _users.SetUserActiveAsync(SelectedUser.Id, !deactivate);
            await LoadAsync();
            StatusMessage = deactivate ? "ব্যবহারকারী নিষ্ক্রিয় করা হয়েছে।" : "ব্যবহারকারী সক্রিয় করা হয়েছে।";
        });
    }

    private async Task ResetPasswordAsync()
    {
        if (SelectedUser is null)
        {
            return;
        }

        var prompt = new PromptWindow("নতুন পাসওয়ার্ড",
            SelectedUser.UserName + "-এর জন্য নতুন পাসওয়ার্ড সেট করুন। ব্যবহারকারী পরবর্তী লগ ইনে এটি পরিবর্তন করতে বাধ্য থাকবে।",
            "নতুন পাসওয়ার্ড *");

        if (prompt.ShowDialog() != true)
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _users.ResetPasswordAsync(SelectedUser.Id, prompt.Value, mustChange: true);
            StatusMessage = "পাসওয়ার্ড পরিবর্তন করা হয়েছে।";
        }, "সংরক্ষণ করা হচ্ছে…");
    }

    private async Task CreateRoleAsync()
    {
        await RunAsync(async () =>
        {
            if (NewRoleName.Trim().Length == 0)
            {
                throw new ValidationException("ভূমিকার নাম দিন।");
            }

            await _users.CreateRoleAsync(NewRoleName.Trim(), null);
            NewRoleName = string.Empty;
            OnPropertyChanged(nameof(NewRoleName));
            await LoadAsync();
            StatusMessage = "ভূমিকা তৈরি করা হয়েছে।";
        }, "তৈরি করা হচ্ছে…");
    }

    private async Task LoadPermissionsAsync()
    {
        if (SelectedRole is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            IReadOnlyList<string> granted = await _users.GetRolePermissionsAsync(SelectedRole.Id);

            Permissions.Clear();
            foreach ((string code, string group, string nameBn) in PermissionCodes.All)
            {
                Permissions.Add(new PermissionItem
                {
                    Code = code,
                    Group = group,
                    NameBn = nameBn,
                    IsGranted = granted.Contains(code, StringComparer.OrdinalIgnoreCase)
                });
            }
        }, "অনুমতি আনা হচ্ছে…");
    }

    private async Task SavePermissionsAsync()
    {
        if (SelectedRole is null)
        {
            return;
        }

        if (SelectedRole.IsSystem
            && !_dialogs.Confirm($"'{SelectedRole.Name}' একটি সিস্টেম ভূমিকা। এর অনুমতি পরিবর্তন করলে "
                + "সব ব্যবহারকারী প্রভাবিত হবে। চালিয়ে যাবেন?", "নিশ্চিত করুন"))
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _users.SetRolePermissionsAsync(SelectedRole.Id,
                Permissions.Where(p => p.IsGranted).Select(p => p.Code).ToList());
            StatusMessage = "অনুমতি সংরক্ষণ করা হয়েছে।";
        }, "সংরক্ষণ করা হচ্ছে…");
    }
}
