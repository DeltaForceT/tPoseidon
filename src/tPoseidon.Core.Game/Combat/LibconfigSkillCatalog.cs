using System.Collections.Generic;
using System.Linq;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Combat;

public sealed class LibconfigSkillCatalog : ISkillCatalog
{
    private readonly ISkillParam2Lookup _lookup;

    public LibconfigSkillCatalog(ISkillParam2Lookup lookup)
    {
        _lookup = lookup;
    }

    public ISkill? GetById(ushort skillId)
    {
        var entry = _lookup.GetById(skillId);
        return entry is null ? null : new LibconfigSkill(entry);
    }

    public IEnumerable<ISkill> All()
        => _lookup.All().Select(e => (ISkill)new LibconfigSkill(e));
}
