using System.Collections.Generic;

namespace tPoseidon.Core.LibConfig;

public interface ISkillParam2Lookup
{
    SkillParam2Entry? GetById(int skillId);
    IEnumerable<SkillParam2Entry> All();
    int Count { get; }
}
