using Microsoft.Dafny;

namespace MutDafny.Mutator;

public abstract class Mutator
{
    public abstract void Mutate(Statement statement);
    
    public abstract void Mutate(Expression expression);
}