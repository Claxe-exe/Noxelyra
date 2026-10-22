using System.Text.Json;
namespace ClaxeDesktop.Core;
public interface ITaskbarService { bool AutoHide { get; } void SetAutoHide(bool value); }
public sealed record TaskbarOwner(int ProcessId,long StartedUtcTicks);
public sealed record TaskbarLease(int Version,TaskbarOwner Owner,bool Previous,bool Applied);
public sealed class TaskbarSession(ITaskbarService platform,string journal,TaskbarOwner owner)
{
    private async Task<FileStream> LockAsync(CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(journal)!);
        for(var i=0;;i++)try{return new FileStream(journal+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}catch(IOException)when(i<40){await Task.Delay(50,token);}
    }
    private TaskbarLease Read()
    {
        if(new FileInfo(journal).Length>2048)throw new InvalidDataException("Görev çubuğu kurtarma kaydı çok büyük.");
        var lease=JsonSerializer.Deserialize<TaskbarLease>(File.ReadAllText(journal))??throw new InvalidDataException();
        if(lease.Version!=1||lease.Owner is null||lease.Owner.ProcessId<=0||lease.Owner.StartedUtcTicks<=0||!lease.Applied)throw new InvalidDataException("Görev çubuğu kurtarma kaydı geçersiz.");return lease;
    }
    private void Write(TaskbarLease lease)
    {
        var temporary=journal+".tmp";using(var stream=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough)){JsonSerializer.Serialize(stream,lease);stream.Flush(true);}File.Move(temporary,journal,true);
    }
    private async Task SetVerifiedAsync(bool value,CancellationToken token)
    {
        platform.SetAutoHide(value);
        for(int i=0;i<12;i++){if(platform.AutoHide==value)return;await Task.Delay(75,token);}
        throw new InvalidOperationException("Windows görev çubuğu değişikliğini doğrulamadı. Görev çubuğu ayarlarından elle değiştirebilirsin.");
    }
    public async Task EnableAsync(Func<Task> ensureGuardian,CancellationToken token=default)
    {
        using var gate=await LockAsync(token);
        TaskbarLease? lease=File.Exists(journal)?Read():null;
        if(lease is not null&&lease.Owner!=owner)throw new IOException("Önceki görev çubuğu kurtarması tamamlanmalı.");
        if(lease is null&&platform.AutoHide)return; // It was already the user's preference; do not own it.
        if(lease is null){lease=new(1,owner,platform.AutoHide,true);Write(lease);}
        await ensureGuardian(); // Journal is durable before either the helper or the mutation starts.
        await SetVerifiedAsync(true,token);
    }
    public async Task RestoreAsync(TaskbarOwner? expectedOwner=null,CancellationToken token=default)
    {
        using var gate=await LockAsync(token);if(!File.Exists(journal))return;
        var lease=Read();if(expectedOwner is not null&&lease.Owner!=expectedOwner)return;
        if(platform.AutoHide==lease.Applied)await SetVerifiedAsync(lease.Previous,token);
        File.Delete(journal);
    }
}
