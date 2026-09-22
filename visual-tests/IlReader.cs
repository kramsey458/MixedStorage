using System.Reflection;
using System.Reflection.Emit;

// Walks a method's IL for the checks that read what the built mod (or the game) does, not only what it declares.
internal static class IlReader
{
    private static readonly Dictionary<short, OpCode> Codes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(f => (OpCode)f.GetValue(null)!).GroupBy(o => o.Value).ToDictionary(g => g.Key, g => g.First());

    // Token is the metadata token of a member, type, string or signature operand, otherwise 0.
    public readonly record struct Instruction(int Offset, OpCode Code, int Token);

    public static List<Instruction> Read(MethodBase method)
    {
        var il = method.GetMethodBody()?.GetILAsByteArray() ?? Array.Empty<byte>();
        var code = new List<Instruction>();
        for (int i = 0; i < il.Length;)
        {
            int offset = i;
            short value = il[i++];
            if (value == 0xFE) value = unchecked((short)(0xFE00 | il[i++]));
            if (!Codes.TryGetValue(value, out var op)) throw new Exception($"Unknown IL opcode 0x{value:X} at {offset} in {method.DeclaringType?.FullName}.{method.Name}.");
            int token = 0;
            switch (op.OperandType)
            {
                case OperandType.InlineNone: break;
                case OperandType.ShortInlineBrTarget: case OperandType.ShortInlineI: case OperandType.ShortInlineVar: i += 1; break;
                case OperandType.InlineVar: i += 2; break;
                case OperandType.InlineI8: case OperandType.InlineR: i += 8; break;
                case OperandType.InlineSwitch: i += 4 + 4 * BitConverter.ToInt32(il, i); break;
                case OperandType.InlineField: case OperandType.InlineMethod: case OperandType.InlineString:
                case OperandType.InlineTok: case OperandType.InlineType: case OperandType.InlineSig:
                    token = BitConverter.ToInt32(il, i); i += 4; break;
                default: i += 4; break;
            }
            code.Add(new Instruction(offset, op, token));
        }
        return code;
    }

    // The member an instruction names (a method for call, callvirt and newobj), or null when it names none or
    // cannot be resolved here.
    public static MemberInfo Member(MethodBase method, Instruction instruction)
    {
        if (instruction.Token == 0 || instruction.Code.OperandType == OperandType.InlineString || instruction.Code.OperandType == OperandType.InlineSig) return null;
        var typeArguments = method.DeclaringType is { IsGenericType: true } type ? type.GetGenericArguments() : null;
        var methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : null;
        try { return method.Module.ResolveMember(instruction.Token, typeArguments, methodArguments); }
        catch (Exception ex) when (ex is ArgumentException || ex is TypeLoadException || ex is FileNotFoundException || ex is BadImageFormatException) { return null; }
    }

    // The methods a method calls directly (call, callvirt, newobj, ldftn, ldvirtftn), as LateGamePerformance reads them.
    public static IEnumerable<MethodBase> Calls(MethodBase method) => Read(method)
        .Where(i => i.Code == OpCodes.Call || i.Code == OpCodes.Callvirt || i.Code == OpCodes.Newobj || i.Code == OpCodes.Ldftn || i.Code == OpCodes.Ldvirtftn)
        .Select(i => Member(method, i) as MethodBase).Where(m => m != null)!;
}
