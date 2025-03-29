# CopyCat
Copies the contents from a source location to a destination location while handling various edge cases efficiently.
Directory Copy Utility

Requirements and Edge Cases:
1. Objective
Develop a C# utility to copy directory contents from a source location to a destination location while handling various edge cases efficiently.
________________________________________
2. Features & Functional Requirements
•	Copy all files and subdirectories from source to destination.
•	Provide user-configurable options for overwrite behavior, symbolic links, logging, and concurrency.
•	Ensure robust error handling and efficient performance for large directories.
________________________________________
3. Edge Cases & Considerations
A. Functional Edge Cases
1. Source Directory Issues
•	Source directory does not exist → Throw an error or return failure?
•	Source is empty → Create an empty destination or skip?
•	Source is a file instead of a directory → Copy it or reject the request?
2. Destination Directory Issues
•	Destination does not exist → Automatically create it or require confirmation?
•	Destination is not empty:
o	Merge with existing files?
o	Overwrite existing files?
o	Rename duplicate files (file_copy.txt, file (1).txt)?
o	Skip existing files?
3. File Handling
•	File name conflicts:
o	Overwrite?
o	Skip?
o	Rename automatically?
•	Hidden/System Files → Should they be copied?
•	Symbolic Links:
o	Copy as links?
o	Resolve to actual files?
•	Special files (executables, DLLs, locked files, etc.)
•	Preserve timestamps, attributes, and permissions?
4. Subdirectory Handling
•	Handle deeply nested structures → Should we enforce a max depth?
•	Detect cyclic symbolic links to prevent infinite loops.
________________________________________
B. Performance Edge Cases
1. Large Directory Handling
•	Process files sequentially or in parallel?
•	Limit max concurrent operations?
2. File Size Considerations
•	Handle large files efficiently → Use FileStream instead of File.Copy().
•	Ensure no memory overflows.
3. Handling Network & External Drives
•	Support UNC paths (\\server\share\folder).
4. Logging & Progress Updates
•	Provide real-time progress updates.
•	Track and log skipped/failed files.
________________________________________
C. Failure & Error Handling
1. File in Use / Locked File
•	Retry? Skip? Log error?
2. Partial Copy & Rollback Strategy
•	Delete partially copied files on failure?
•	Allow partial copies to remain?
3. Disk Space Constraints
•	Check available space before copying.
4. Interruption Handling
•	Resume safely if the process is interrupted.
________________________________________
4. Configurable Options (User Decisions Needed)
•	Overwrite behavior: Merge / Overwrite / Skip / Rename
•	Symbolic links: Copy as is / Resolve to actual file
•	Hidden files: Include / Exclude
•	Logging: Enable / Disable / Log level (Verbose, Errors only, etc.)
•	Parallel processing: Enable / Disable / Max concurrency
•	Error handling: Fail fast / Continue and log errors
________________________________________
5. Implementation Considerations
•	Concurrency Strategy: Use Parallel.ForEach() for multi-threading.
•	Error Handling: Fail fast or log and continue based on user choice.
•	Logging: Use Serilog or Console.WriteLine().
•	File Handling: Use FileStream for better performance on large files.

Build and run cmd: CopyCat.exe --source "C:\Source" --destination "D:\Backup" --overwrite true --log verbose --parallel true


