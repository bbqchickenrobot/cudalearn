#define _SIZE_T_DEFINED 
#ifndef __CUDACC__ 
#define __CUDACC__ 
#endif 
#ifndef __cplusplus 
#define __cplusplus 
#endif

#include <cuda.h> 
#include <device_launch_parameters.h> 
#include <texture_fetch_functions.h> 
#include <builtin_types.h> 
#include <vector_functions.h> 
#include "float.h" 




template< typename T >
__device__ void vectorEquals(const T *A, const T *B, bool *C, int numElements, T epsilon)
{
	int i = blockDim.x * blockIdx.x + threadIdx.x;

	// A global result.
	if (i == 0)
		C[0] = true;

	__syncthreads();

	if (i < numElements)
	{
		// Calculate the difference. 
		float result = A[i] - B[i];
		result = result >= 0 ? result : -result;

		// We dont care who wins. We will only signal C[0] when this value changes. So there is no need for atomics.
		if (result > epsilon)
			C[0] = false;
	}
}

/**
* Vector addition: C = A + B.
*
* This sample is a very basic sample that implements element by element
* vector addition. It is the same as the sample illustrating Chapter 2
* of the programming guide with some additions like error checking.
*/
template< typename T >
__device__ void vectorAdd(const T *A, const T *B, T *C, int numElements)
{
	int i = blockDim.x * blockIdx.x + threadIdx.x;

	if (i < numElements)
	{
		C[i] = A[i] + B[i];
	}
}

extern "C"  
{

	__global__ void vectorAdd1f(const float *A, const float *B, float *C, int numElements)
	{
		vectorAdd<float>(A, B, C, numElements);
	}

	__global__ void vectorAdd1d(const float *A, const float *B, float *C, int numElements)
	{
		vectorAdd<float>(A, B, C, numElements);
	}

	__global__ void vectorAdd1i(const float *A, const float *B, float *C, int numElements)
	{
		vectorAdd<float>(A, B, C, numElements);
	}


	__global__ void vectorEquals1f(const float *A, const float *B, bool *C, int numElements, float epsilon)
	{
		vectorEquals<float>(A, B, C, numElements, epsilon);
	}

	__global__ void vectorEquals1d(const double *A, const double *B, bool *C, int numElements, double epsilon)
	{
		vectorEquals<double>(A, B, C, numElements, epsilon);
	}

	__global__ void vectorEquals1i(const int *A, const int *B, bool *C, int numElements)
	{
		vectorEquals<int>(A, B, C, numElements, 0);
	}
}



