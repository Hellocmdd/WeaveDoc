using WeaveDoc.App.Tests.Fakes;
using WeaveDoc.App.ViewModels;
using WeaveDoc.Converter.Config;
using Xunit;

namespace WeaveDoc.App.Tests;

public sealed class LiteratureViewModelTests
{
    private static BibtexEntry Entry(string key, string type, params (string, string)[] fields)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (f, v) in fields) dict[f] = v;
        return new BibtexEntry { EntryType = type, CitationKey = key, Fields = dict };
    }

    [Fact]
    public async Task ImportBibTextAsync_LoadsEntriesIntoList()
    {
        var repo = new FakeLiteratureRepository();
        var vm = new LiteratureViewModel(repo);

        await vm.ImportBibTextAsync("@article{smith2024, author = {Smith}, title = {A Study}, journal = {Nature}, year = {2024}, volume = {1}, pages = {1-2}}", "refs.bib");

        Assert.Single(vm.Entries);
        Assert.Equal("smith2024", vm.Entries[0].CitationKey);
        Assert.Equal("A Study", vm.Entries[0].Title);
        Assert.False(vm.Entries[0].HasMissingFields);
    }

    [Fact]
    public async Task ImportBibTextAsync_FlagsEntriesWithMissingFields()
    {
        var repo = new FakeLiteratureRepository();
        var vm = new LiteratureViewModel(repo);

        // article 缺 volume 和 pages
        await vm.ImportBibTextAsync("@article{k, author = {A}, title = {T}, journal = {J}, year = {2024}}", "refs.bib");

        Assert.True(vm.Entries[0].HasMissingFields);
    }

    [Fact]
    public async Task ImportBibTextAsync_MultipleEntries_AllListed()
    {
        var repo = new FakeLiteratureRepository();
        var vm = new LiteratureViewModel(repo);

        await vm.ImportBibTextAsync(
            "@article{a, author={A}, title={Ta}, journal={J}, year={2024}, volume={1}, pages={1}}\n" +
            "@book{b, author={B}, title={Tb}, publisher={P}, year={2023}}", "refs.bib");

        Assert.Equal(2, vm.Entries.Count);
    }

    [Fact]
    public async Task RefreshAsync_ReloadsAllFromRepository()
    {
        var repo = new FakeLiteratureRepository();
        repo.Seed(new LiteratureEntryRecord { CitationKey = "seeded", EntryType = "book", Title = "Seeded" });
        var vm = new LiteratureViewModel(repo);

        await vm.RefreshAsync();

        Assert.Single(vm.Entries);
        Assert.Equal("seeded", vm.Entries[0].CitationKey);
    }

    [Fact]
    public async Task SearchAsync_FiltersEntries()
    {
        var repo = new FakeLiteratureRepository();
        var vm = new LiteratureViewModel(repo);
        await vm.ImportBibTextAsync(
            "@article{alpha, author={A}, title={Neural Nets}, journal={J}, year={2024}, volume={1}, pages={1}}\n" +
            "@book{beta, author={B}, title={Trees}, publisher={P}, year={2023}}", "refs.bib");

        await vm.SearchAsync("neural");

        Assert.Single(vm.Entries);
        Assert.Equal("alpha", vm.Entries[0].CitationKey);
    }

    [Fact]
    public async Task SearchAsync_EmptyQuery_ShowsAll()
    {
        var repo = new FakeLiteratureRepository();
        var vm = new LiteratureViewModel(repo);
        await vm.ImportBibTextAsync(
            "@article{alpha, author={A}, title={X}, journal={J}, year={2024}, volume={1}, pages={1}}\n" +
            "@book{beta, author={B}, title={Y}, publisher={P}, year={2023}}", "refs.bib");

        await vm.SearchAsync("");

        Assert.Equal(2, vm.Entries.Count);
    }

    [Fact]
    public async Task DeleteAsync_RemovesEntry()
    {
        var repo = new FakeLiteratureRepository();
        var vm = new LiteratureViewModel(repo);
        await vm.ImportBibTextAsync("@article{k, author={A}, title={T}, journal={J}, year={2024}, volume={1}, pages={1}}", "refs.bib");

        await vm.DeleteAsync("k");

        Assert.Empty(vm.Entries);
        Assert.Contains("k", repo.DeleteCalls);
    }

    [Fact]
    public async Task UpdateFieldAsync_PersistsViaRepository()
    {
        var repo = new FakeLiteratureRepository();
        var vm = new LiteratureViewModel(repo);
        await vm.ImportBibTextAsync("@article{k, author={A}, title={T}, journal={J}, year={2024}, volume={1}, pages={1}}", "refs.bib");

        await vm.UpdateFieldAsync("k", "pages", "5-9");

        Assert.Contains(("k", "pages", "5-9"), repo.UpdateFieldCalls);
    }

    [Fact]
    public async Task Operations_SetIsBusyDuringExecution()
    {
        var repo = new FakeLiteratureRepository();
        var vm = new LiteratureViewModel(repo);
        var busyDuring = new List<bool>();

        vm.IsBusyChanged += () => busyDuring.Add(vm.IsBusy);

        await vm.RefreshAsync();

        // 至少经历了 置忙→完成 的翻转
        Assert.Contains(true, busyDuring);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task ImportBibTextAsync_UpdatesStatusText()
    {
        var repo = new FakeLiteratureRepository();
        var vm = new LiteratureViewModel(repo);

        await vm.ImportBibTextAsync("@article{k, author={A}, title={T}, journal={J}, year={2024}, volume={1}, pages={1}}", "refs.bib");

        Assert.False(string.IsNullOrEmpty(vm.StatusText));
    }
}
