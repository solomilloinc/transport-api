# Refactor Candidates (.NET / C#)

After TDD cycle, look for:

- **Duplication** → Extract method or class
- **Long methods** → Break into private helpers (keep tests on public interface)
- **Shallow modules** → Combine or deepen
- **Feature envy** → Move logic to where data lives
- **Primitive obsession** → Introduce value objects or strongly-typed IDs
- **Existing code** the new code reveals as problematic
- **Fat interfaces** → Split into focused interfaces (ISP)
- **God classes** → Extract focused services with single responsibility
