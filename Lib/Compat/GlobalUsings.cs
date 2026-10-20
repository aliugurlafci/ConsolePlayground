// System.Threading.Lock .NET 9 ile geldi; önceki hedeflerde klasik nesne kilidi kullanılır.
#if !NET9_0_OR_GREATER
global using Lock = object;
#endif
